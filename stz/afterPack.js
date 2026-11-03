// Иконка и сведения о версии в STZ.exe (resedit, чистый JS — работает и в Linux)
const fs = require('fs'), path = require('path');
module.exports = async (ctx) => {
  if (ctx.electronPlatformName !== 'win32') return;
  const ResEdit = await import('resedit').then(m => m.default || m);
  const ver = require('./package.json').version;
  const exe = path.join(ctx.appOutDir, 'STZ.exe');
  const pe = ResEdit.NtExecutable.from(fs.readFileSync(exe), { ignoreCert: true });
  const res = ResEdit.NtExecutableResource.from(pe);
  const ico = ResEdit.Data.IconFile.from(fs.readFileSync(path.join(__dirname, 'stz.ico')));
  ResEdit.Resource.IconGroupEntry.replaceIconsForResource(res.entries, 1, 1033, ico.icons.map(i => i.data));
  const vi = ResEdit.Resource.VersionInfo.fromEntries(res.entries)[0] || ResEdit.Resource.VersionInfo.createEmpty();
  const n = ver.split(/[.-]/).map(Number).filter(x => !isNaN(x));
  vi.setFileVersion(n[0] || 1, n[1] || 0, n[2] || 0, n[3] || 0, 1033); vi.setProductVersion(n[0] || 1, n[1] || 0, n[2] || 0, n[3] || 0, 1033);
  vi.setStringValues({ lang: 1033, codepage: 1200 }, {
    FileDescription: 'STZ Расчёт зубьев', ProductName: 'STZ Расчёт зубьев', CompanyName: 'Рафаил Шарапов',
    LegalCopyright: '© 2026 Рафаил Шарапов · rafail@sharapov-cnc.ru', OriginalFilename: 'STZ.exe', InternalName: 'STZ', FileVersion: ver, ProductVersion: ver
  });
  vi.outputToResourceEntries(res.entries);
  res.outputResource(pe);
  fs.writeFileSync(exe, Buffer.from(pe.generate()));
  console.log('  • иконка и версия записаны в', exe);
};
