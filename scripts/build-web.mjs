import { cpSync, existsSync, mkdirSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { resolve } from 'node:path'
import { spawnSync } from 'node:child_process'

const root = fileURLToPath(new URL('../', import.meta.url))
const frontend = resolve(root, 'frontend')
const build = spawnSync(process.execPath, [resolve(frontend, 'node_modules/vite/bin/vite.js'), 'build'], {
  cwd: frontend, stdio: 'inherit',
})
if (build.error) throw build.error
if (build.status !== 0) process.exit(build.status ?? 1)
const output = resolve(frontend, 'dist')
if (!existsSync(resolve(output, 'index.html'))) throw new Error('Missing frontend build')
const webroot = resolve(root, 'src/Rcc.Api/wwwroot')
mkdirSync(webroot, { recursive: true })
cpSync(output, webroot, { recursive: true })
console.log('Frontend copiado a src/Rcc.Api/wwwroot')
