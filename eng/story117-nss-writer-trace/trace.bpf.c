/* Metadata only. Linux 6.6 signatures; this program has NOT been kernel validated.
 * Source references and coverage limitations are in README.md.
 * No user buffer, argv, environment, or file contents are dereferenced.
 */
#include <uapi/linux/ptrace.h>
#include <linux/fs.h>
#include <linux/sched.h>
#include <linux/mm.h>
#include <linux/binfmts.h>
#include <linux/cred.h>

#define DEADLINE_NS @DEADLINE_NS@ULL
#define BOOT_DEADLINE_NS @BOOT_DEADLINE_NS@ULL
#define OUTPUT_DEV @OUTPUT_DEV@U
#define OUTPUT_INO @OUTPUT_INO@ULL
#define PARTS 12
#define PART_LEN 48
#define PATH_READ_ERROR 1
#define PATH_TRUNCATED 2
#define PATH_MOUNT_RELATIVE 4
#define EVENT_SKIPPED 8
#define IDENTITY_ERROR 16
#define OUTPUT_ANCESTRY_UNCERTAIN 32
#define IDENTITY_NO_MM 64
#define IDENTITY_KTHREAD 128
#define IDENTITY_EXIT_EXE_UNAVAILABLE 256
#define EXT4_MAGIC 0xef53

struct path_meta {
    u64 ino;
    u64 parent_ino;
    u64 size;
    u64 fs_magic;
    u32 dev;
    u32 mode;
    u32 flags;
    u32 count;
    char parts[PARTS][PART_LEN]; /* leaf first; never invent a host mount prefix */
};

struct identity {
    u64 start_boottime;
    u64 parent_start_boottime;
    u32 pid;
    u32 tid;
    u32 ppid;
    u32 uid;
    u32 gid;
    u32 flags;
    char comm[TASK_COMM_LEN];
    struct path_meta exe;
};

struct event {
    u64 entry_ns;
    u64 return_ns;
    u64 entry_boot_ns;
    u64 return_boot_ns;
    s64 result;
    u64 detail;
    u64 requested_size;
    u32 op;
    u32 flags;
    u32 old_pid;
    u32 reserved;
    struct identity process;
    struct identity fork_creator;
    struct path_meta source;
    struct path_meta destination;
    struct path_meta target_after;
};

struct pair_key { u64 pidtid; u32 op; u32 padding; };
struct context { struct event event; struct dentry *target; struct file *target_file; };
BPF_HASH(pairs, struct pair_key, struct context, 4096);
BPF_PERCPU_ARRAY(scratch, struct context, 1);
BPF_ARRAY(counters, u64, 12);
BPF_ARRAY(enabled, u64, 1);
BPF_PERF_OUTPUT(events);

/* counters: submissions, submit errors, map errors, overwrites, unmatched
 * returns, read errors, truncated paths, deadline pairs, excluded, starts,
 * successful pair completions, identity errors. */
static __always_inline void count(u32 slot)
{
    u64 *value = counters.lookup(&slot);
    if (value) __sync_fetch_and_add(value, 1);
}

static __always_inline int read_meta(struct path_meta *out, struct dentry *d)
{
    struct inode *inode = NULL;
    struct super_block *sb = NULL;
    struct dentry *parent = NULL;
    out->flags = PATH_MOUNT_RELATIVE;
    if (!d || bpf_probe_read_kernel(&inode, sizeof(inode), &d->d_inode)) {
        out->flags |= PATH_READ_ERROR;
        count(5);
        return 0;
    }
    /* A negative destination dentry legitimately has no pre-operation inode. */
    if (inode) {
        if (bpf_probe_read_kernel(&out->ino, sizeof(out->ino), &inode->i_ino) ||
            bpf_probe_read_kernel(&out->mode, sizeof(inode->i_mode), &inode->i_mode) ||
            bpf_probe_read_kernel(&out->size, sizeof(out->size), &inode->i_size) ||
            bpf_probe_read_kernel(&sb, sizeof(sb), &inode->i_sb)) {
            out->flags |= PATH_READ_ERROR;
        }
    }
    if (bpf_probe_read_kernel(&parent, sizeof(parent), &d->d_parent)) out->flags |= PATH_READ_ERROR;
    if (parent) {
        struct inode *pi = NULL;
        if (bpf_probe_read_kernel(&pi, sizeof(pi), &parent->d_inode) || !pi) {
            out->flags |= PATH_READ_ERROR;
        } else {
            if (bpf_probe_read_kernel(&out->parent_ino, sizeof(out->parent_ino), &pi->i_ino)) out->flags |= PATH_READ_ERROR;
            if (!sb && bpf_probe_read_kernel(&sb, sizeof(sb), &pi->i_sb)) out->flags |= PATH_READ_ERROR;
        }
    }
    if (sb) {
        if (bpf_probe_read_kernel(&out->dev, sizeof(out->dev), &sb->s_dev) ||
            bpf_probe_read_kernel(&out->fs_magic, sizeof(out->fs_magic), &sb->s_magic))
            out->flags |= PATH_READ_ERROR;
    } else out->flags |= PATH_READ_ERROR;

    int rooted = 0;
#pragma unroll
    for (int n = 0; n < PARTS; n++) {
        if (!d || rooted) continue;
        struct qstr name = {};
        struct dentry *up = NULL;
        if (bpf_probe_read_kernel(&name, sizeof(name), &d->d_name) ||
            bpf_probe_read_kernel(&up, sizeof(up), &d->d_parent)) {
            out->flags |= PATH_READ_ERROR;
            rooted = 1;
            continue;
        }
        if (name.len >= PART_LEN) out->flags |= PATH_TRUNCATED;
        if (bpf_probe_read_kernel_str(out->parts[n], PART_LEN, name.name) < 0)
            out->flags |= PATH_READ_ERROR;
        out->count = n + 1;
        if (up == d) rooted = 1;
        d = up;
    }
    if (!rooted) out->flags |= PATH_TRUNCATED;
    if (out->flags & PATH_READ_ERROR) count(5);
    if (out->flags & PATH_TRUNCATED) count(6);
    return 0;
}

static __always_inline int excluded(struct dentry *d)
{
    /* The only exclusion is ancestry beneath this collector's new directory.
     * Internal kernel dev_t uses 12 major + 20 minor bits; Python renders it.
     * Uncertain ancestry never excludes a workload event. */
#pragma unroll
    for (int n = 0; n < 32; n++) {
        if (!d) return 0;
        struct inode *inode = NULL;
        struct super_block *sb = NULL;
        struct dentry *up = NULL;
        u64 ino = 0;
        u32 dev = 0;
        if (bpf_probe_read_kernel(&inode, sizeof(inode), &d->d_inode)) return -1;
        if (inode) {
            if (bpf_probe_read_kernel(&ino, sizeof(ino), &inode->i_ino) ||
                bpf_probe_read_kernel(&sb, sizeof(sb), &inode->i_sb) || !sb ||
                bpf_probe_read_kernel(&dev, sizeof(dev), &sb->s_dev)) return -1;
            if (ino == OUTPUT_INO && dev == OUTPUT_DEV) return 1;
        }
        if (bpf_probe_read_kernel(&up, sizeof(up), &d->d_parent)) return -1;
        if (d == up) return 0;
        d = up;
    }
    return -1;
}

static __always_inline void identify(struct identity *out, struct task_struct *task, u32 op)
{
    struct task_struct *leader = NULL, *parent = NULL, *parent_leader = NULL;
    struct mm_struct *mm = NULL;
    struct file *exe = NULL;
    struct dentry *dentry = NULL;
    unsigned int task_flags = 0;
    if (bpf_probe_read_kernel(&leader, sizeof(leader), &task->group_leader) || !leader)
        goto error;
    if (bpf_probe_read_kernel(&out->start_boottime, sizeof(out->start_boottime), &leader->start_boottime) ||
        bpf_probe_read_kernel(&out->pid, sizeof(out->pid), &task->tgid) ||
        bpf_probe_read_kernel(&out->tid, sizeof(out->tid), &task->pid) ||
        bpf_probe_read_kernel(out->comm, sizeof(out->comm), &task->comm) ||
        bpf_probe_read_kernel(&parent, sizeof(parent), &leader->real_parent) || !parent ||
        bpf_probe_read_kernel(&parent_leader, sizeof(parent_leader), &parent->group_leader) || !parent_leader ||
        bpf_probe_read_kernel(&out->ppid, sizeof(out->ppid), &parent_leader->tgid) ||
        bpf_probe_read_kernel(&out->parent_start_boottime, sizeof(out->parent_start_boottime), &parent_leader->start_boottime) ||
        bpf_probe_read_kernel(&mm, sizeof(mm), &task->mm) ||
        bpf_probe_read_kernel(&task_flags, sizeof(task_flags), &task->flags)) goto error;
    if (task_flags & PF_KTHREAD) out->flags |= IDENTITY_KTHREAD;
    if (task == (struct task_struct *)bpf_get_current_task()) {
        u64 uidgid = bpf_get_current_uid_gid();
        out->uid = (u32)uidgid;
        out->gid = uidgid >> 32;
    } else {
        const struct cred *cred = NULL;
        if (bpf_probe_read_kernel(&cred, sizeof(cred), &task->real_cred) || !cred ||
            bpf_probe_read_kernel(&out->uid, sizeof(out->uid), &cred->uid.val) ||
            bpf_probe_read_kernel(&out->gid, sizeof(out->gid), &cred->gid.val)) goto error;
    }
    if (!mm) {
        out->flags |= IDENTITY_NO_MM;
        if (task_flags & PF_KTHREAD) return;
        if (op == 103) { out->flags |= IDENTITY_EXIT_EXE_UNAVAILABLE; return; }
        goto error;
    }
    if (bpf_probe_read_kernel(&exe, sizeof(exe), &mm->exe_file)) goto error;
    if (!exe) {
        if (op == 103) { out->flags |= IDENTITY_EXIT_EXE_UNAVAILABLE; return; }
        goto error;
    }
    if (bpf_probe_read_kernel(&dentry, sizeof(dentry), &exe->f_path.dentry)) goto error;
    read_meta(&out->exe, dentry);
    if (out->exe.flags & (PATH_READ_ERROR | PATH_TRUNCATED)) goto error;
    return;
error:
    out->flags |= IDENTITY_ERROR;
    count(11);
}

static __always_inline struct event *fresh(u32 op)
{
    u64 now = bpf_ktime_get_ns();
    u64 boot_now = bpf_ktime_get_boot_ns();
    if (now >= DEADLINE_NS || boot_now >= BOOT_DEADLINE_NS) return NULL;
    u32 zero = 0;
    u64 *on = enabled.lookup(&zero);
    if (!on || !*on) return NULL;
    struct context *context = scratch.lookup(&zero);
    if (!context) { count(2); return NULL; }
    __builtin_memset(context, 0, sizeof(*context));
    struct event *e = &context->event;
    e->op = op;
    e->entry_ns = now;
    e->entry_boot_ns = boot_now;
    identify(&e->process, (struct task_struct *)bpf_get_current_task(), op);
    return e;
}

static __always_inline int remember(struct event *e, struct dentry *src, struct dentry *dst, int force_skip)
{
    if (!e) return 0;
    int exsrc = src ? excluded(src) : 0;
    int exdst = dst ? excluded(dst) : 0;
    if (exsrc < 0 || exdst < 0) e->flags |= OUTPUT_ANCESTRY_UNCERTAIN;
    if (force_skip || (exsrc == 1 && (!dst || exdst == 1)) || (!src && exdst == 1)) {
        e->flags |= EVENT_SKIPPED;
        count(8);
    } else {
        if (src) read_meta(&e->source, src);
        if (dst) read_meta(&e->destination, dst);
    }
    struct pair_key key = { .pidtid = bpf_get_current_pid_tgid(), .op = e->op };
    struct context *context = (struct context *)e;
    /* d_move/d_exchange move the source dentry to the destination name. The
     * saved destination may instead be negative/displaced at return. */
    context->target = e->op == 6 ? src : (dst ? dst : src);
    if (pairs.lookup(&key)) count(3);
    if (pairs.update(&key, context)) count(2);
    else count(9);
    return 0;
}

static __always_inline int file_entry(u32 op, struct file *file, struct file *dst, u64 detail)
{
    struct event *e = fresh(op);
    if (!e) return 0;
    e->detail = detail;
    struct dentry *d = NULL, *to = NULL;
    if (file && bpf_probe_read_kernel(&d, sizeof(d), &file->f_path.dentry)) {
        e->flags |= PATH_READ_ERROR; count(5);
    }
    if (dst && bpf_probe_read_kernel(&to, sizeof(to), &dst->f_path.dentry)) {
        e->flags |= PATH_READ_ERROR; count(5);
    }
    struct inode *inode = NULL;
    umode_t mode = 0;
    struct dentry *target = dst ? to : d;
    if (!target || bpf_probe_read_kernel(&inode, sizeof(inode), &target->d_inode) || !inode ||
        bpf_probe_read_kernel(&mode, sizeof(mode), &inode->i_mode)) {
        e->flags |= PATH_READ_ERROR; count(5);
    }
    return remember(e, d, to, inode && !S_ISREG(mode));
}

static __always_inline int finish(struct pt_regs *ctx, u32 op)
{
    struct pair_key key = { .pidtid = bpf_get_current_pid_tgid(), .op = op };
    struct context *context = pairs.lookup(&key);
    u64 now = bpf_ktime_get_ns();
    u64 boot_now = bpf_ktime_get_boot_ns();
    u32 zero = 0;
    u64 *on = enabled.lookup(&zero);
    if (!on || !*on) return 0; /* retain unfinished pairs after gate closure */
    if (!context) { if (now < DEADLINE_NS && boot_now < BOOT_DEADLINE_NS) count(4); return 0; }
    struct event *e = &context->event;
    if (now >= DEADLINE_NS || boot_now >= BOOT_DEADLINE_NS) count(7);
    else if (on && *on && !(e->flags & EVENT_SKIPPED)) {
        e->return_ns = now;
        e->return_boot_ns = boot_now;
        if ((op >= 5 && op <= 12) || op == 16 || op == 17)
            e->result = (s64)(s32)PT_REGS_RC(ctx); /* int, sign-extended */
        else if (op == 201 || op == 202)
            e->result = (s64)(u32)PT_REGS_RC(ctx); /* unsigned vm_fault_t */
        else e->result = (s64)PT_REGS_RC(ctx); /* signed-long byte results */
        if (e->result == 0 && context->target_file) {
            if (bpf_probe_read_kernel(&context->target, sizeof(context->target), &context->target_file->f_path.dentry)) {
                e->flags |= PATH_READ_ERROR; count(5);
            }
        }
        if ((e->result > 0 && e->op <= 17) || e->result == 0)
            read_meta(&e->target_after, context->target);
        if (bpf_ktime_get_ns() < DEADLINE_NS && bpf_ktime_get_boot_ns() < BOOT_DEADLINE_NS) {
            count(0);
            if (events.perf_submit(ctx, e, sizeof(*e))) count(1);
        } else count(7);
    }
    if (pairs.delete(&key)) count(2);
    count(10);
    return 0;
}

/* Explicit 6.6 entry signatures. Buffer/iterator arguments are never read. */
int enter_vfs_write(struct pt_regs *ctx, struct file *file) { return file_entry(1, file, NULL, 0); }
int enter_kernel_write(struct pt_regs *ctx, struct file *file) { return file_entry(2, file, NULL, 0); }
int enter_iter_write(struct pt_regs *ctx, struct file *file) { return file_entry(3, file, NULL, 0); }
int enter_vfs_iter_write(struct pt_regs *ctx, struct file *file) { return file_entry(4, file, NULL, 0); }
int enter_notify_change(struct pt_regs *ctx, struct mnt_idmap *idmap, struct dentry *dentry, struct iattr *attr)
{
    struct event *e = fresh(5);
    if (!e) return 0;
    unsigned int valid = 0;
    if (!attr || bpf_probe_read_kernel(&valid, sizeof(valid), &attr->ia_valid)) {
        e->flags |= PATH_READ_ERROR; count(5);
    }
    if (valid & ATTR_SIZE) {
        if (bpf_probe_read_kernel(&e->requested_size, sizeof(e->requested_size), &attr->ia_size)) {
            e->flags |= PATH_READ_ERROR; count(5);
        }
    }
    e->detail = valid;
    return remember(e, dentry, NULL, 0);
}
int enter_rename(struct pt_regs *ctx, struct renamedata *rd)
{
    struct event *e = fresh(6);
    if (!e) return 0;
    struct dentry *old = NULL, *new = NULL;
    if (!rd || bpf_probe_read_kernel(&old, sizeof(old), &rd->old_dentry) ||
        bpf_probe_read_kernel(&new, sizeof(new), &rd->new_dentry) ||
        bpf_probe_read_kernel(&e->detail, sizeof(rd->flags), &rd->flags)) {
        e->flags |= PATH_READ_ERROR; count(5);
    }
    return remember(e, old, new, 0);
}
int enter_link(struct pt_regs *ctx, struct dentry *old, struct mnt_idmap *idmap, struct inode *dir, struct dentry *new)
{ return remember(fresh(7), old, new, 0); }
int enter_unlink(struct pt_regs *ctx, struct mnt_idmap *idmap, struct inode *dir, struct dentry *dentry)
{ return remember(fresh(8), dentry, NULL, 0); }
int enter_create(struct pt_regs *ctx, struct mnt_idmap *idmap, struct inode *dir, struct dentry *dentry)
{ return remember(fresh(9), NULL, dentry, 0); }
int enter_mkdir(struct pt_regs *ctx, struct mnt_idmap *idmap, struct inode *dir, struct dentry *dentry)
{ return remember(fresh(10), NULL, dentry, 0); }
int enter_rmdir(struct pt_regs *ctx, struct mnt_idmap *idmap, struct inode *dir, struct dentry *dentry)
{ return remember(fresh(11), dentry, NULL, 0); }
int enter_fallocate(struct pt_regs *ctx, struct file *file, int mode) { return file_entry(12, file, NULL, (u64)mode); }
int enter_copy(struct pt_regs *ctx, struct file *src, loff_t off, struct file *dst)
{ return file_entry(13, src, dst, 0); }
int enter_splice(struct pt_regs *ctx, struct file *src, loff_t *off, struct file *dst)
{ return file_entry(14, src, dst, 0); }
int enter_splice_file(struct pt_regs *ctx, struct pipe_inode_info *pipe, struct file *dst)
{ return file_entry(15, NULL, dst, 0); }
int enter_mknod(struct pt_regs *ctx, struct mnt_idmap *idmap, struct inode *dir, struct dentry *dentry)
{ return remember(fresh(16), NULL, dentry, 0); }
int enter_tmpfile(struct pt_regs *ctx, struct mnt_idmap *idmap, const struct path *parent, struct file *file)
{
    struct event *e = fresh(17);
    if (!e) return 0;
    struct dentry *directory = NULL;
    if (!parent || bpf_probe_read_kernel(&directory, sizeof(directory), &parent->dentry)) {
        e->flags |= PATH_READ_ERROR; count(5);
    }
    struct context *context = (struct context *)e;
    context->target_file = file;
    return remember(e, directory, NULL, 0);
}

#define RETURN_FN(name, number) int name(struct pt_regs *ctx) { return finish(ctx, number); }
RETURN_FN(return_vfs_write, 1)
RETURN_FN(return_kernel_write, 2)
RETURN_FN(return_iter_write, 3)
RETURN_FN(return_vfs_iter_write, 4)
RETURN_FN(return_notify_change, 5)
RETURN_FN(return_rename, 6)
RETURN_FN(return_link, 7)
RETURN_FN(return_unlink, 8)
RETURN_FN(return_create, 9)
RETURN_FN(return_mkdir, 10)
RETURN_FN(return_rmdir, 11)
RETURN_FN(return_fallocate, 12)
RETURN_FN(return_copy, 13)
RETURN_FN(return_splice, 14)
RETURN_FN(return_splice_file, 15)
RETURN_FN(return_mknod, 16)
RETURN_FN(return_tmpfile, 17)

static __always_inline int lifecycle(void *ctx, u32 op, struct task_struct *task, u32 old_pid, struct task_struct *creator)
{
    struct event *e = fresh(op);
    if (!e) return 0;
    if (creator) identify(&e->fork_creator, creator, op);
    __builtin_memset(&e->process, 0, sizeof(e->process));
    identify(&e->process, task, op);
    e->old_pid = old_pid;
    e->return_ns = e->entry_ns;
    e->return_boot_ns = e->entry_boot_ns;
    if (bpf_ktime_get_ns() < DEADLINE_NS && bpf_ktime_get_boot_ns() < BOOT_DEADLINE_NS) {
        count(0);
        if (events.perf_submit(ctx, e, sizeof(*e))) count(1);
    } else count(7);
    return 0;
}
int raw_fork(struct bpf_raw_tracepoint_args *ctx)
{ return lifecycle(ctx, 101, (struct task_struct *)ctx->args[1], 0, (struct task_struct *)ctx->args[0]); }
int raw_exec(struct bpf_raw_tracepoint_args *ctx)
{ return lifecycle(ctx, 102, (struct task_struct *)ctx->args[0], (u32)ctx->args[1], NULL); }
int raw_exit(struct bpf_raw_tracepoint_args *ctx)
{ return lifecycle(ctx, 103, (struct task_struct *)ctx->args[0], 0, NULL); }

static __always_inline int page_evidence(struct pt_regs *ctx, struct vm_fault *vmf, u32 op)
{
    struct event *e = fresh(op);
    if (!e) return 0;
    struct vm_area_struct *vma = NULL;
    struct file *file = NULL;
    struct dentry *d = NULL;
    if (!vmf || bpf_probe_read_kernel(&vma, sizeof(vma), &vmf->vma) || !vma ||
        bpf_probe_read_kernel(&file, sizeof(file), &vma->vm_file) || !file ||
        bpf_probe_read_kernel(&d, sizeof(d), &file->f_path.dentry)) {
        e->flags |= PATH_READ_ERROR; count(5);
    }
    return remember(e, d, NULL, 0);
}
int enter_generic_mkwrite(struct pt_regs *ctx, struct vm_fault *vmf) { return page_evidence(ctx, vmf, 201); }
int enter_ext4_mkwrite(struct pt_regs *ctx, struct vm_fault *vmf) { return page_evidence(ctx, vmf, 202); }
RETURN_FN(return_generic_mkwrite, 201)
RETURN_FN(return_ext4_mkwrite, 202)
