const {chromium}=require('playwright');(async()=>{const b=await chromium.launch();const p=await b.newPage({viewport:{width:1400,height:900}});const errs=[];p.on('pageerror',e=>errs.push(e.message+' '+(e.stack||'').split('\n')[1]));p.on('console',m=>{if(m.type()==='error'&&!/404|Failed/.test(m.text()))errs.push(m.text())});
await p.goto('http://127.0.0.1:8765/index.html');await p.waitForTimeout(1500);
await p.evaluate(()=>loadExample(4));await p.waitForTimeout(400);
const r=await p.evaluate(()=>{const c=LAST;return {err:c.err,note:c.note,passes:PASSES.map(p=>p[0]),PV:PASSES.map(x=>{const v=passVals(c,x[0]);return [x[0],v.head,v.razv,v.bab,v.md,v.stol,v.ecc,v.lul,v.del,v.obk,v.chk,f(v.U,2)].join(' | ')})}});
console.log(JSON.stringify(r,null,1));
for(const t of ['calc','card','guide','a4','spot','git']){await p.evaluate(t=>showTab(t),t);await p.waitForTimeout(300);await p.screenshot({path:`/home/claude/shots/g_${t}.png`,fullPage:t==='calc'||t==='card'});}
await p.evaluate(()=>{showTab('spot');GSEL.k='d1';buildSpot()});await p.screenshot({path:'/home/claude/shots/g_spot2.png',fullPage:true});
await p.evaluate(()=>g19Apply('d1'));console.log('corr',await p.evaluate(()=>[passVals(LAST,'F1').md,passVals(LAST,'F1').stol,JSON.stringify(CORRG)]));
await p.evaluate(()=>mmOpen('F1'));for(let i=0;i<2;i++)await p.evaluate(()=>mmGo(1));await p.screenshot({path:'/home/claude/shots/g_mm.png'});await p.evaluate(()=>mmClose());
console.log('rt',await p.evaluate(()=>{const j=collect();newJob();apply(j);calc();return [LAST.g19,JSON.stringify(CORRG),PASSES.length]}));
console.log('switch',await p.evaluate(()=>{loadExample(3);const a=[LAST.hyp,PASSES.length];loadExample(1);return a.concat([!!LAST.g19,PASSES.length,dm(LAST.e)])}));
await p.evaluate(()=>{loadExample(4);});await p.emulateMedia({media:'print'});await p.evaluate(()=>{showTab('a4');markPrint('a4')});await p.pdf({path:'/home/claude/shots/g_a4.pdf',format:'A4'});await p.emulateMedia({media:'screen'});
await p.evaluate(()=>{showTab('card');markPrint('card')});await p.emulateMedia({media:'print'});await p.pdf({path:'/home/claude/shots/g_card.pdf',format:'A4',landscape:true});
console.log(errs);await b.close()})();
