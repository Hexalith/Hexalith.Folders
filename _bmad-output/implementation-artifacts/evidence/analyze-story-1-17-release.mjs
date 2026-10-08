// Read-only reproduction of the proposed version; does not run semantic-release.
import {execFileSync} from 'node:child_process';
import {createHash} from 'node:crypto';
import {readFileSync} from 'node:fs';
import {analyzeCommits} from '@semantic-release/commit-analyzer';

const revision = '02152bace0c782b2b65bf9ccd31e4738a94eac17';
const range = `v1.1.1..${revision}`;
const config = JSON.parse(readFileSync('.releaserc.json', 'utf8'));
const plugin = config.plugins.find(p => (Array.isArray(p) ? p[0] : p) === '@semantic-release/commit-analyzer');
if (!plugin) throw new Error('Configured commit analyzer is missing.');
const options = Array.isArray(plugin) ? plugin[1] : {};
const raw = execFileSync('git', ['log', range, '--format=%H%x00%B%x00'], {encoding: 'utf8'}).split('\0');
const commits = [];
for (let i = 0; i + 1 < raw.length; i += 2) {
    commits.push({hash: raw[i].trim(), message: raw[i + 1].trim()});
}
const releaseType = await analyzeCommits(options, {commits, cwd: process.cwd(), logger: {log() {}}});
const versions = {};
for (const name of ['@semantic-release/commit-analyzer', 'semantic-release', 'conventional-changelog-angular', 'conventional-commits-parser']) {
    versions[name] = JSON.parse(readFileSync(`node_modules/${name}/package.json`, 'utf8')).version;
}
const digest = path => createHash('sha256').update(readFileSync(path)).digest('hex');
process.stdout.write(JSON.stringify({range, node_version: process.versions.node, analyzer_options: options,
    commit_count: commits.length, release_type: releaseType,
    proposed_version: {minor: '1.2.0', patch: '1.1.2', major: '2.0.0'}[releaseType] ?? null,
    package_versions: versions, release_config_sha256: digest('.releaserc.json'),
    package_lock_sha256: digest('package-lock.json')}, null, 2) + '\n');
