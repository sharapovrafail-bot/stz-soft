// Ставит иконку и сведения о версии в exe без wine (resedit, чистый JS)
const fs = require('fs'), path = require('path');
module.exports = async (ctx) => {
  if (ctx.electronPlatformName !== 'win32') return;
  const ResEdit = await import('resedit').then(m => m.default || m);
  const exe = path.join(ctx.appOutDir, 'SimulyatorUP.exe');
  const data = fs.readFileSync(exe);
  const pe = ResEdit.NtExecutable.from(data, { ignoreCert: true });
  const res = ResEdit.NtExecutableResource.from(pe);
  const ico = ResEdit.Data.IconFile.from(fs.readFileSync(path.join(__dirname, 'build', 'icon.ico')));
  ResEdit.Resource.IconGroupEntry.replaceIconsForResource(res.entries, 1, 1033, ico.icons.map(i => i.data));
  const vi = ResEdit.Resource.VersionInfo.fromEntries(res.entries)[0] || ResEdit.Resource.VersionInfo.createEmpty();
  vi.setFileVersion(9, 0, 1, 0, 1033); vi.setProductVersion(9, 0, 1, 0, 1033);
  vi.setStringValues({ lang: 1033, codepage: 1200 }, {
    FileDescription: 'Симулятор УП Fanuc', ProductName: 'Симулятор УП Fanuc', CompanyName: 'Rafail Sharapov',
    LegalCopyright: '© 2026 Рафаил Шарапов · rafail@sharapov-cnc.ru', OriginalFilename: 'SimulyatorUP.exe', InternalName: 'SimulyatorUP',
    FileVersion: '9.0.1', ProductVersion: '9.0.1'
  });
  vi.outputToResourceEntries(res.entries);
  res.outputResource(pe);
  fs.writeFileSync(exe, Buffer.from(pe.generate()));
  console.log('  • иконка и версия записаны в', exe);
};
