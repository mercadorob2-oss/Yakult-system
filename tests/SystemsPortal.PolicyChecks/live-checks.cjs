const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const os=require('node:os');
const crypto=require('node:crypto');
const {chromium}=require(path.join(os.tmpdir(),'yakult-learning-qa/node_modules/playwright'));
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:path.join(process.env.LOCALAPPDATA,'ms-playwright/chromium-1228/chrome-win64/chrome.exe')});
 try {
 const page=await browser.newPage({viewport:{width:1440,height:1000},reducedMotion:'reduce'});
 const errors=[];page.on('pageerror',e=>errors.push(e.message));
 const base='http://192.168.100.186:7016';
 const response=await page.goto(base+'/company-policies/it-policy---employee-guidelines');assert.equal(response.status(),200);
 const body=page.locator('[data-policy-document]');
 assert.equal(await body.locator(':scope>section').count(),7);
 assert.equal(await body.locator('img').count(),9);
 const baseline=fs.readFileSync('artifacts/it-policy-upgrade/live-before.html','utf8');
 const missing=await page.evaluate(html=>{
  const original=new DOMParser().parseFromString(html,'text/html');
  const clean=s=>s.replace(/\s+/g,' ').trim();
  return [...original.querySelectorAll('.it-info-section')].flatMap(section=>{
   const current=document.getElementById(section.id);const text=clean(current.textContent);
   return [...section.querySelectorAll('h2,h3,summary,p,li')].map(el=>clean(el.textContent)).filter(s=>s&&s!=='Process paths'&&!text.includes(s));
  });
 },baseline);
 assert.deepEqual(missing,[],'Previously live instructions missing');
 const assets=[];
 for(const asset of ['css/company-policy-document.css','css/company-policy-editor.css','js/company-policy-reader.js','js/company-policy-builder.js']){
  const r=await page.request.get(base+'/'+asset);assert.equal(r.status(),200);
  const actual=crypto.createHash('sha256').update(await r.body()).digest('hex');
  const expected=crypto.createHash('sha256').update(fs.readFileSync('Yakult.SystemsPortal/wwwroot/'+asset)).digest('hex');
  assert.equal(actual,expected,'Live asset mismatch: '+asset);assets.push({asset,sha256:actual});
 }
 const versions=await page.locator('link[href*="company-policy-document"],script[src*="company-policy-reader"]').evaluateAll(els=>els.map(el=>el.getAttribute('href')||el.getAttribute('src')));
 assert.ok(versions.length===2&&versions.every(url=>url.includes('?v=')));
 for(const img of await body.locator('img').all()){await img.scrollIntoViewIfNeeded();await img.evaluate(i=>i.decode());assert.ok(await img.evaluate(i=>i.naturalWidth>0));}
 await page.locator('[data-policy-search]').fill('Kawasaki');assert.ok(await body.locator('mark').count()>0);assert.equal(await body.locator('section[hidden]').count(),0);
 await page.locator('[data-policy-clear]').click();
 const link=page.locator('[data-policy-image]').first();await link.click();await page.locator('dialog[open]').waitFor();
 await page.locator('dialog img').evaluate(image=>image.decode());
 assert.ok(await page.locator('dialog').evaluate(dialog=>{
  const box=dialog.getBoundingClientRect();
  return Math.abs(box.x+box.width/2-innerWidth/2)<2 && Math.abs(box.y+box.height/2-innerHeight/2)<2;
 }), 'Enlarged image viewer must be centered in the viewport');await page.keyboard.press('ArrowRight');assert.match(await page.locator('dialog figcaption').innerText(),/^2 of 9/);await page.keyboard.press('Escape');assert.equal(await link.evaluate(el=>el===document.activeElement),true);
 await page.setViewportSize({width:390,height:844});await page.locator('[data-policy-jump]').selectOption('guest-internet');assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
 assert.equal(await page.getByText('Print guidelines',{exact:true}).count(),0);
 const protectedPreview=await page.request.post(base+'/Admin/CompanyInfo/Policy/Preview',{form:{Title:'Read-only verification',Body:'test'}});
 assert.ok(protectedPreview.status()===400||protectedPreview.status()===401||protectedPreview.status()===403||protectedPreview.url().includes('/Home/Login'));
 assert.deepEqual(errors,[]);
 fs.writeFileSync('artifacts/it-policy-upgrade/live-checks.json',JSON.stringify({status:'passed',url:response.url(),procedures:7,images:9,approvedLiveContentPreserved:true,assets,versions,previewRequiresAuthentication:true,javaScriptErrors:errors},null,2));
 console.log('PASS: live URL, approved content, nine images, asset hashes/versions, search, image viewer, mobile layout, and protected preview');
 } finally {await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1});
