import { copyFile, mkdir } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const root = fileURLToPath(new URL('../', import.meta.url));
const destination = path.join(root, 'src/BranchCompliance.Web/wwwroot/vendor');
await mkdir(destination, { recursive: true });
for (const [source, target] of [
    ['admin-lte/dist/css/adminlte.min.css', 'adminlte.min.css'],
    ['admin-lte/dist/js/adminlte.min.js', 'adminlte.min.js'],
    ['admin-lte/LICENSE', 'AdminLTE-LICENSE'],
    ['bootstrap/dist/js/bootstrap.bundle.min.js', 'bootstrap.bundle.min.js'],
    ['bootstrap/LICENSE', 'Bootstrap-LICENSE'],
    ['@popperjs/core/LICENSE.md', 'Popper-LICENSE.md'],
]) {
    await copyFile(path.join(root, 'node_modules', source), path.join(destination, target));
}
console.log('Copied only the production theme, Bootstrap bundle, and license notices.');
