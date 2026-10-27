// hubasic2sl のブラウザ向けの一式 (scripts/build-hubasic-wasm.sh が作るもの) を node で読み込み、
// wasm の変換が同じ版の CLI と同じかを確かめる。1 つでも違えば終了コード 1。
//
//   node hubasic-wasm-smoke.mjs <一式のフォルダ> <リポジトリ> <hubasic2sl.dll>
//
// 確かめること: examples/hubasic/*.BAS の SL と診断 (オプションあり・なし)、Shift_JIS の入力、error と warning の診断、
// 束の testdata の .SL、CheckLayout の境目、Version の版と SHA が manifest と合うこと。
// 改行は LF にそろえて比べる (Windows の CLI は CRLF で書く)。node は 22.7 以上 (.js の ES モジュールの dotnet.js を読むため)。
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { pathToFileURL } from 'node:url';

const [bundle, repo, cliDll] = process.argv.slice(2);
if (!bundle || !repo || !cliDll) {
  console.error('使い方: node hubasic-wasm-smoke.mjs <一式のフォルダ> <リポジトリ> <hubasic2sl.dll>');
  process.exit(2);
}

const [major, minor] = process.versions.node.split('.').map(Number);
if (major < 22 || (major === 22 && minor < 7)) {
  console.error(`hubasic-wasm-smoke: node ${process.versions.node} は古いので確かめられません (22.7 以上が要ります)`);
  process.exit(1);
}

const { dotnet } = await import(pathToFileURL(path.join(bundle, '_framework', 'dotnet.js')).href);
const runtime = await dotnet.create();
const exports = await runtime.getAssemblyExports('hubasic2sl-wasm');
const api = exports.SLANGCompiler.HuBasic.Wasm.HuBasicWeb;
const manifest = JSON.parse(fs.readFileSync(path.join(bundle, 'manifest.json'), 'utf8'));

const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'hubasic-wasm-smoke-'));
let failed = 0;
const lf = s => s === null ? null : s.replace(/\r\n/g, '\n');
const check = (ok, what) => {
  console.log(`${ok ? 'ok  ' : 'NG  '} ${what}`);
  if (!ok) failed++;
};

// CLI で変換して、SL (できなければ null) と stderr の行を返す
function cli(bytes, name, args) {
  const input = path.join(tmp, name);
  const output = path.join(tmp, 'OUT.SL');
  fs.writeFileSync(input, bytes);
  fs.rmSync(output, { force: true });
  const r = spawnSync('dotnet', [cliDll, input, '-o', output, ...args], { encoding: 'utf8' });
  if (r.error) throw r.error;
  const sl = r.status === 0 ? lf(fs.readFileSync(output, 'utf8')) : null;
  return { sl, err: r.stderr.split(/\r?\n/).filter(l => l.length > 0) };
}

function compare(bytes, name, options, args, what) {
  const web = JSON.parse(api.Transpile(bytes, name, JSON.stringify(options)));
  const ref = cli(bytes, name, args);
  const same = lf(web.sl) === ref.sl
    && JSON.stringify(web.diagnostics.map(d => d.text)) === JSON.stringify(ref.err)
    && web.ok === (ref.sl !== null);
  check(same, what);
}

try {
  const dir = path.join(repo, 'examples', 'hubasic');
  for (const f of fs.readdirSync(dir).filter(f => /\.BAS$/i.test(f)).sort()) {
    const bytes = fs.readFileSync(path.join(dir, f));
    compare(bytes, f, {}, [], `${f}`);
    compare(bytes, f, { wait: 'line:480', stringLen: 63, noLineNumbers: true },
            ['--wait', 'line:480', '--string-len', '63', '--no-line-numbers'], `${f} (wait・stringLen・noLineNumbers)`);
  }
  // Shift_JIS の半角カナ (ｱｲﾝ)
  const sjis = Buffer.concat([Buffer.from('9 REM\r\n10 PRINT "'), Buffer.from([0xB1, 0xB2, 0xDD]), Buffer.from('"\r\n')]);
  compare(sjis, 'SJIS.BAS', { inputEncoding: 'sjis' }, ['--input-encoding', 'sjis'], 'Shift_JIS の入力');
  // 診断 (ない行への GOTO の error と、何もしない KBUF の warning)
  compare(Buffer.from('10 KBUF ON\n20 GOTO 99\n'), 'ERR.BAS', {}, [], '診断 (error と warning)');

  // 束の testdata の .SL は、同じ .BAS を wasm で変換したものと同じ
  for (const c of manifest.testdata.cases) {
    const web = JSON.parse(api.Transpile(fs.readFileSync(path.join(bundle, c.bas)), path.basename(c.bas),
                                         JSON.stringify(manifest.testdata.transpileOptions)));
    check(web.ok && lf(web.sl) === lf(fs.readFileSync(path.join(bundle, c.sl), 'utf8')) && web.diskMainName === c.diskMainName,
          `testdata ${c.name} の .SL と起動名`);
  }

  const fit = JSON.parse(api.CheckLayout(0xEFE0, 0x2000));
  const over = JSON.parse(api.CheckLayout(0xEFE1, 0x2000));
  check(fit.ok === true && over.ok === false && over.message.includes('HB0312'), 'CheckLayout の境目 ($FFE0 で収まり、$FFE1 で HB0312)');

  const v = JSON.parse(api.Version());
  check(v.version === manifest.hubasic2slVersion, `Version の版 ${v.version} が manifest と合う`);
  check(v.informational.endsWith('+' + manifest.slangCompilerRevision), `Version の SHA が manifest (${manifest.slangCompilerRevision}) と合う`);
} finally {
  fs.rmSync(tmp, { recursive: true, force: true });
}

if (failed > 0) {
  console.error(`hubasic-wasm-smoke: ${failed} 個が違いました`);
  process.exit(1);
}
console.log('hubasic-wasm-smoke: すべて同じでした');
process.exit(0);
