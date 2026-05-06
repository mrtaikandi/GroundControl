import { spawn } from 'node:child_process';
import { access, mkdir } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const projectDirectory = resolve(scriptDirectory, '..');
const defaultInputPath = resolve(projectDirectory, '../../src/GroundControl.Api/OpenApi.json');
const outputPath = resolve(projectDirectory, 'src/api/types.ts');
const input = process.env.OPENAPI_INPUT ?? defaultInputPath;
const cliPath = resolve(projectDirectory, 'node_modules/openapi-typescript/bin/cli.js');

await mkdir(dirname(outputPath), { recursive: true });

if (!/^https?:\/\//i.test(input)) {
  await access(input);
}

const generator = spawn(process.execPath, [cliPath, input, '-o', outputPath], {
  cwd: projectDirectory,
  stdio: 'inherit',
});

const exitCode = await new Promise<number | null>((resolveExitCode) => {
  generator.on('exit', resolveExitCode);
});

if (exitCode !== 0) {
  throw new Error(`openapi-typescript exited with code ${exitCode}.`);
}