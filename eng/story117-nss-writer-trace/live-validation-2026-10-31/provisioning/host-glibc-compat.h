/* Preserve the pre-C23 string interface expected by Linux 6.6 host tools.
 * This affects host userspace compilation only; no kernel configuration,
 * generated release, source or warning policy is changed. */
#ifndef _GNU_SOURCE
#define _GNU_SOURCE
#endif
#include <features.h>
#undef __GLIBC_USE_ISOC23
#define __GLIBC_USE_ISOC23 0
