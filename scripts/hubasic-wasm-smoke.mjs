// ブラウザ向けの一式 (scripts/build-hubasic-wasm.sh が作るもの) を node で読み込み、
// wasm の変換 (HuBasicWeb)・コンパイルとディスク作り (SlangWeb) が同じ版の CLI と同じかを確かめる。1 つでも違えば終了コード 1。
//
//   node hubasic-wasm-smoke.mjs <一式のフォルダ> <リポジトリ> <hubasic2sl.dll> <slangc.dll>
//
// 確かめること:
// - hubasic2sl: examples/hubasic/*.BAS の SL と診断 (オプションあり・なし)、Shift_JIS の入力、error と warning の診断、
//   束の testdata の .SL、CheckLayout の境目、Version の版・組の印・SHA が manifest と合うこと
// - slangc: 束の env・runtime・include を PutFile で入れ、testdata の .SL・x1 の env の見本・error のソース・無い env を
//   wasm の Compile と CLI (リポジトリの全部の runtime と include で動かす) で比べる。SLANG タブ用の testdata の .ASM と同じこと。
//   BuildHuDisk のディスクが testdata の .d88 と同じこと。slangc の Version の版と SHA が manifest と合うこと
// 改行は LF にそろえて比べる (Windows の CLI は CRLF で書く)。node は 22.7 以上 (.js の ES モジュールの dotnet.js を読むため)。
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { pathToFileURL } from 'node:url';

// 道は呼んだ人の作業フォルダからの相対でもよい (CLI を一時の dir で動かすので、先に絶対の道にする)
const [bundle, repo, cliDll, slangcDll] = process.argv.slice(2).map(p => p && path.resolve(p));
if (!bundle || !repo || !cliDll || !slangcDll) {
  console.error('使い方: node hubasic-wasm-smoke.mjs <一式のフォルダ> <リポジトリ> <hubasic2sl.dll> <slangc.dll>');
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

// CLI が stderr に書く「ファイルの命令を使うので、slangbuild -E x1native_hufs で…」の行は、wasm では結果の requiredEnv で返す
const envNote = l => l.startsWith('hubasic2sl: ファイルの命令を使うので');

function compare(bytes, name, options, args, what) {
  const web = JSON.parse(api.Transpile(bytes, name, JSON.stringify(options)));
  const ref = cli(bytes, name, args);
  const same = lf(web.sl) === ref.sl
    && JSON.stringify(web.diagnostics.map(d => d.text)) === JSON.stringify(ref.err.filter(l => !envNote(l)))
    && web.requiredEnv === (ref.err.map(l => envNote(l) && /slangbuild -E (\S+)/.exec(l)).find(m => m)?.[1] ?? null)
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

  // 束の testdata (と hufsTestdata) の .SL は、同じ .BAS を wasm で変換したものと同じ
  const groups = [manifest.testdata, manifest.hufsTestdata];
  for (const g of groups) for (const c of g.cases) {
    const web = JSON.parse(api.Transpile(fs.readFileSync(path.join(bundle, c.bas)), path.basename(c.bas),
                                         JSON.stringify(g.transpileOptions)));
    check(web.ok && lf(web.sl) === lf(fs.readFileSync(path.join(bundle, c.sl), 'utf8')) && web.diskMainName === c.diskMainName,
          `testdata ${c.name} の .SL と起動名`);
  }

  // ---------------------------------------------------------------- slangc (SlangWeb)
  // 束の env・runtime・include を、ディスクの上と同じ絶対の道でメモリの中のファイルシステムに入れる
  const slang = exports.SLANGCompiler.HuBasic.Wasm.SlangWeb;
  const putTree = (dir) => {
    for (const ent of fs.readdirSync(dir, { withFileTypes: true })) {
      const p = path.join(dir, ent.name);
      if (ent.isDirectory()) putTree(p);
      else slang.PutFile(p, new Uint8Array(fs.readFileSync(p)));
    }
  };
  for (const d of ['env', 'runtime', 'include']) putTree(path.join(bundle, d));

  // CLI の slangc の期待の値: リポジトリの全部の runtime と include で動かす (束の写し漏れも見つかるように)。
  // 一時の dir を CWD・HOME にし、SLANG_HOME を消して、-L <リポジトリ> -I <リポジトリ>/include だけを見る
  function cliSlangc(src, env) {
    const out = path.join(tmp, 'SLANGOUT.ASM');
    for (const f of fs.readdirSync(tmp).filter(f => f.startsWith('SLANGOUT'))) fs.rmSync(path.join(tmp, f), { force: true });
    const childEnv = { ...process.env, HOME: tmp };
    delete childEnv.SLANG_HOME;
    const r = spawnSync('dotnet', [slangcDll, '-E', env, '-L', repo, '-I', path.join(repo, 'include'), '-o', out, src],
                        { cwd: tmp, env: childEnv, encoding: 'utf8' });
    if (r.error) throw r.error;
    const read = f => fs.existsSync(f) ? lf(fs.readFileSync(f, 'utf8')) : null;
    const overlays = fs.readdirSync(tmp).filter(f => /^SLANGOUT\..+\.ASM$/.test(f)).sort()
      .map(f => ({ name: f.slice('SLANGOUT.'.length, -'.ASM'.length), asm: read(path.join(tmp, f)) }));
    const log = r.stderr.split(/\r?\n/).filter(l => l.length > 0 && !l.startsWith('; Environment:') && !l.startsWith('; Output:'));
    return {
      ok: r.status === 0,
      asm: env === 'c64' ? null : read(out),
      overlays,
      modules: read(path.join(tmp, 'SLANGOUT.modules')),
      inc: read(path.join(tmp, 'SLANGOUT.inc')),
      log,
    };
  }

  // wasm の slangc: 束の runtime・env・include だけを見る。ソースは、そのソースのある dir ごと同じ絶対の道で入れる
  const uploaded = new Set();
  function wasmSlangc(src, env) {
    const dir = path.dirname(src);
    if (!uploaded.has(dir)) {
      for (const f of fs.readdirSync(dir)) {
        const p = path.join(dir, f);
        if (fs.statSync(p).isFile()) slang.PutFile(p, new Uint8Array(fs.readFileSync(p)));
      }
      uploaded.add(dir);
    }
    const r = JSON.parse(slang.Compile(src, JSON.stringify({ env, libPaths: [bundle], includePaths: [path.join(bundle, 'include')] })));
    // include の中の診断は、束の include の道を書く。CLI (リポジトリの include) と比べるために道を揃える
    const fix = s => s.split(path.join(bundle, 'include')).join(path.join(repo, 'include'));
    return {
      ok: r.ok,
      asm: r.asm === null ? null : lf(r.asm),
      overlays: r.overlays.map(o => ({ name: o.name, asm: lf(o.asm) })).sort((a, b) => a.name < b.name ? -1 : 1),
      modules: r.modules === null ? null : r.modules.join('\n') + '\n',
      inc: r.inc === null ? null : lf(r.inc),
      log: r.log.map(fix),
    };
  }

  function compareSlangc(src, env, what, expectOk) {
    const a = cliSlangc(src, env);
    const b = wasmSlangc(src, env);
    const same = JSON.stringify(a) === JSON.stringify(b);
    const okAsExpected = a.ok === expectOk && !b.log.some(l => l.includes('Runtime not found'));
    check(same && okAsExpected, `slangc ${what} (${env})${same ? '' : ' 違う'}${okAsExpected ? '' : ` 期待は ${expectOk ? '成功' : '失敗'}`}`);
  }

  // 束の testdata・hufsTestdata の HuBASIC の .SL (それぞれの env)
  for (const g of groups) for (const c of g.cases)
    compareSlangc(path.join(bundle, c.sl), g.env, `testdata ${c.name}.SL`, true);

  // x1 の env で組む見本。全部、成功するはず (成功しない見本が出たら、ここの一覧を直す)
  const X1_EXAMPLES = [
    'CPMIOTEST.SL', 'FMANDEL.SL', 'FURUI.SL', 'MAGICSMPL.SL', 'MANDEL.SL', 'MODTEST_RESIDENT.SL', 'MODTEST.SL', 'MSXLSX.SL',
    'MSXROM.SL', 'MSXSPDRV2.SL', 'MSXSPDRV.SL', 'MZ25IOCS.SL', 'PC80mk2.SL', 'STARS.SL', 'STARS_X1.SL', 'X1CGROM.SL', 'X1GRP.SL',
    'X1NATIVE_HELLO.SL', 'X1SGL_PRINT_TEST.SL', 'X1SGL.SL', 'chip/CHIPTEST.SL', 'spr/SPRTEST2.SL', 'spr/SPRTEST3.SL',
    'spr/SPRTEST.SL', 'tile/TILETEST.SL', 'tilespr/TILSPRTEST.SL', 'ui/UITEST.SL',
  ];
  const listed = [
    ...fs.readdirSync(path.join(repo, 'examples')).filter(f => /\.SL$/.test(f)),
    ...['chip', 'spr', 'tile', 'tilespr', 'ui'].flatMap(d => fs.readdirSync(path.join(repo, 'examples', d)).filter(f => /\.SL$/.test(f)).map(f => `${d}/${f}`)),
  ].sort();
  // x1 の env では CLI で組めるが、束に入れない include (X1 向けでない MSXGRP.LIB) を使う見本。
  // wasm では、その include が無いと言って止まる (束が X1 向けの include だけを持つことの確かめ)
  const NEEDS_NON_X1_INCLUDE = ['MSXLSX.SL', 'MSXROM.SL', 'MSXSPDRV2.SL', 'MSXSPDRV.SL'];
  check(JSON.stringify(listed) === JSON.stringify([...X1_EXAMPLES].sort()), 'x1 の見本の一覧が examples と合う');
  for (const f of X1_EXAMPLES) {
    const src = path.join(repo, 'examples', f);
    if (NEEDS_NON_X1_INCLUDE.includes(f)) {
      const a = cliSlangc(src, 'x1');
      const b = wasmSlangc(src, 'x1');
      check(a.ok && !b.ok && b.log.some(l => l.includes('MSXGRP.LIB')), `slangc ${f} (x1): 束に無い include で止まる`);
    } else {
      compareSlangc(src, 'x1', f, true);
    }
  }

  // error になるソースと、無い env
  const errSl = path.join(tmp, 'ERR.SL');
  fs.writeFileSync(errSl, 'VAR J;\nMAIN()\nBEGIN\n  J = ;\nEND;\n');
  slang.PutFile(errSl, new Uint8Array(fs.readFileSync(errSl)));
  compareSlangc(errSl, 'x1', 'error のソース', false);
  compareSlangc(errSl, 'nosuchenv', '無い env', false);

  // SLANG タブ用の testdata: wasm の ASM が、束の .ASM (CLI の slangbuild で組んだもの) と同じ
  for (const c of manifest.slangTestdata.cases) {
    const b = wasmSlangc(path.join(bundle, c.sl), manifest.slangTestdata.env);
    check(b.ok && b.asm === lf(fs.readFileSync(path.join(bundle, c.asm), 'utf8')), `SLANG タブの testdata ${c.name} の .ASM`);
  }

  // Hu-BASIC の形のディスク: wasm の BuildHuDisk が、testdata の .d88 (slangbuild --emit disk --hufs-add で作ったもの) と同じ
  const fileio = manifest.hufsTestdata.cases.find(c => c.name === 'FILEIO');
  check(manifest.hufsTestdata.env === 'x1native_hufs' && fileio?.hufsAdd?.some(a => a.name === 'GREET.TXT' && a.kind === 'text'),
        'hufsTestdata に、GREET.TXT を足した FILEIO がある (ディスクの比べ合わせを飛ばさない)');
  check(!manifest.testdata.cases.some(c => c.name === 'FILEIO'), 'testdata (x1native_slfs) に FILEIO が混ざっていない');
  for (const c of manifest.hufsTestdata.cases) {
    const env = manifest.envs[manifest.hufsTestdata.env];
    const add = c.hufsAdd.map(a => `${a.name}=${path.join(bundle, a.file)},${a.kind}`);
    for (const a of c.hufsAdd) slang.PutFile(path.join(bundle, a.file), new Uint8Array(fs.readFileSync(path.join(bundle, a.file))));
    const r = JSON.parse(slang.BuildHuDisk(new Uint8Array(fs.readFileSync(path.join(bundle, c.bin))),
      JSON.stringify({ mainName: c.diskMainName, load: env.diskMainLoad, exec: env.diskMainExec, add })));
    check(r.ok && Buffer.from(r.d88, 'base64').equals(fs.readFileSync(path.join(bundle, c.d88))), `testdata ${c.name} の .d88 (BuildHuDisk)`);
  }

  const sv = JSON.parse(slang.Version());
  check(sv.slangc === manifest.slangcVersion, `slangc の版 ${sv.slangc} が manifest と合う`);
  check(sv.informational === `${manifest.slangcVersion}+${manifest.slangCompilerRevision}`, `slangc の SHA が manifest と合う`);

  const fit = JSON.parse(api.CheckLayout(0xEFE0, 0x2000));
  const over = JSON.parse(api.CheckLayout(0xEFE1, 0x2000));
  check(fit.ok === true && over.ok === false && over.message.includes('HB0312'), 'CheckLayout の境目 ($FFE0 で収まり、$FFE1 で HB0312)');

  const v = JSON.parse(api.Version());
  check(v.version === manifest.hubasic2slVersion, `Version の版 ${v.version} が manifest と合う`);
  check(v.basAbi === manifest.basAbi, `Version の組の印 ${v.basAbi} が manifest と合う`);
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
