const assert = require('node:assert/strict');
const path = require('node:path');
const fs = require('node:fs');
const os = require('node:os');
const { chromium } = require(path.join(os.tmpdir(), 'yakult-learning-qa/node_modules/playwright'));
const base = 'http://127.0.0.1:5188';
const route = '/company-policies/it-policy---employee-guidelines';
const out = path.resolve('artifacts/it-policy-upgrade');
const clean = s => s.replace(/\s+/g,' ').trim();
(async () => {
 const browser = await chromium.launch({headless:true,executablePath:path.join(process.env.LOCALAPPDATA,'ms-playwright/chromium-1228/chrome-win64/chrome.exe')});
 try {
 const ctx = await browser.newContext({viewport:{width:1440,height:1000},reducedMotion:'reduce',extraHTTPHeaders:{'X-Policy-Test-Role':'ContentEditor'}});
 await ctx.addInitScript(() => document.addEventListener('DOMContentLoaded', () => {
  const style=document.createElement('style');style.textContent='html,body,*{scroll-behavior:auto!important}';document.head.append(style);
 }));
 const page = await ctx.newPage();
 const errors=[];page.on('pageerror',e=>errors.push(e.message));
 const response=await page.goto(base+route);assert.equal(response.status(),200);
 const doc=await (await ctx.request.get(base+'/fixture/document')).json();
 const content=page.locator('[data-policy-document]');
 assert.equal(await content.locator(':scope > section').count(),7);
 assert.equal(await content.locator('img').count(),9);
 for(const section of doc.sections){
  const text=clean(await page.locator('#'+section.id).innerText());
  assert.ok(text.includes(clean(section.title)));
  const texts=[];
  for(const b of section.blocks){
   if(b.text)texts.push(b.text);
   texts.push(...(b.items||[]),...(b.paths||[]));
  }
  for(const t of texts) {
   const plain=clean(t.replace(/\*\*/g,'').replace(/^\s*- /gm,''));
   assert.ok(text.includes(plain), 'Missing approved instruction: '+plain);
  }
 }
 const sourceImages=doc.sections.flatMap(s=>s.blocks.filter(b=>b.type==='flowchart').map(b=>b.imageUrl));
 assert.deepEqual(await content.locator('img').evaluateAll(imgs=>imgs.map(i=>i.getAttribute('src'))),sourceImages);
 for(const img of await content.locator('img').all()) {
  await img.scrollIntoViewIfNeeded();await img.evaluate(i=>i.decode());
  assert.ok(await img.evaluate(i=>i.naturalWidth>0&&i.width>0&&i.getAttribute('width')==i.naturalWidth&&i.getAttribute('height')==i.naturalHeight));
 }
 assert.equal(await page.locator('#guest-internet .policy-paired-steps>li').count(),7);
 assert.equal(await page.locator('#guest-internet .company-policy-flowchart__paths').count(),0);
 assert.equal(await page.locator('.policy-callout').count(),6);
 assert.equal(await page.getByText('Print guidelines',{exact:true}).count(),0);
 const expected=clean(await content.innerText());
 await page.locator('[data-policy-search]').fill('Sir Kawasaki');
 assert.ok(await content.locator('mark').count()>0);
 assert.equal(await content.locator('section[hidden]').count(),0);
 assert.ok(await page.locator('[data-policy-results] a').count()>0);
 assert.equal(clean(await content.innerText()),expected);
 await page.locator('[data-policy-search]').fill('[.*');
 assert.equal(await content.locator('mark').count(),0);
 await page.locator('[data-policy-clear]').click();
 assert.equal(await content.locator('mark').count(),0);
 await page.locator('.it-info-outline details a[href="#internet"]').click();
 await page.waitForFunction(()=>document.querySelector('.it-info-outline a[aria-current="location"]')?.hash==='#internet');
 assert.equal(await page.locator(':focus').getAttribute('id'),'policy-section-internet');
 await page.locator('#software-access').evaluate(s=>s.scrollIntoView());
 await page.waitForFunction(()=>document.querySelector('.it-info-outline a[aria-current="location"]')?.hash==='#software-access');
 const imageLink=page.locator('[data-policy-image]').first();
 await imageLink.click();await page.locator('dialog[open]').waitFor();
 await page.locator('dialog img').evaluate(image=>image.decode());
 assert.ok(await page.locator('dialog').evaluate(dialog=>{
  const box=dialog.getBoundingClientRect();
  return Math.abs(box.x+box.width/2-innerWidth/2)<2 && Math.abs(box.y+box.height/2-innerHeight/2)<2;
 }), 'Enlarged image viewer must be centered in the viewport');
 await page.keyboard.press('ArrowRight');
 assert.match(await page.locator('dialog figcaption').innerText(),/^2 of 9/);
 await page.locator('[data-image-zoom]').click();
 assert.equal(await page.locator('[data-image-zoom]').getAttribute('aria-pressed'),'true');
 await page.keyboard.press('Escape');
 assert.equal(await imageLink.evaluate(el=>el===document.activeElement),true);
 await page.locator('#guest-internet').evaluate(s=>s.scrollIntoView());
 const desktopPair=await page.locator('.policy-paired-steps>li').first().evaluate(li=>({copy:li.querySelector('.policy-step-copy').getBoundingClientRect().toJSON(),image:li.querySelector('figure').getBoundingClientRect().toJSON()}));
 assert.ok(desktopPair.image.x>desktopPair.copy.x+desktopPair.copy.width,'Desktop screenshots should be beside instructions');
 await page.screenshot({path:path.join(out,'desktop-guest.png')});
 await page.locator('#accounts').evaluate(s=>s.scrollIntoView());await page.screenshot({path:path.join(out,'desktop-accounts.png')});
 for(const width of [390,320,768]) {
  await page.setViewportSize({width,height:844});
  await page.locator('[data-policy-jump]').selectOption('guest-internet');
  assert.equal(await page.locator(':focus').getAttribute('id'),'policy-section-guest-internet');
  assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'Horizontal overflow at '+width);
  const pair=await page.locator('.policy-paired-steps>li').first().evaluate(li=>({copy:li.querySelector('.policy-step-copy').getBoundingClientRect().toJSON(),image:li.querySelector('figure').getBoundingClientRect().toJSON()}));
  if(width<640)assert.ok(pair.image.y>=pair.copy.y+pair.copy.height,'Mobile screenshot should stack');
  if(width===390)await page.screenshot({path:path.join(out,'mobile-guest.png')});
 }
 await page.setViewportSize({width:1440,height:1000});
 await page.goto(base+'/Admin/CompanyInfo/Policy/1');
 await page.waitForFunction(()=>document.querySelector('#policyPreviewStatus')?.textContent==='Preview up to date');
 assert.equal(await page.locator('#policyPreview [data-policy-document] img').count(),9);
 const body=await page.locator('#policyBody').inputValue();
 assert.deepEqual(JSON.parse(body.slice('yakult-policy:v1:'.length)),doc,'Editor round-trip changed approved content');
 await page.locator('#Title').fill('Policy **preview** <img src=x onerror=alert(1)>');
 await page.waitForFunction(()=>document.querySelector('#policyPreview h1')?.textContent.includes('<img'));
 assert.equal(await page.locator('#policyPreview h1 img').count(),0);
 await page.locator('#Title').fill('IT Policy & Employee Guidelines');
 await page.waitForFunction(()=>document.querySelector('#policyPreviewStatus')?.textContent==='Preview up to date');
 const publicBody=expected;
 assert.equal(clean(await page.locator('#policyPreview [data-policy-document]').innerText()),publicBody,'Public and preview content differ');
 const before=await page.locator('#policyPreview').innerHTML();
 const requirement=page.locator('[data-editor-format="profile-account"] textarea').first();
 const original=await requirement.inputValue();await requirement.fill('');
 await page.waitForFunction(()=>document.querySelector('#policyPreviewStatus')?.textContent.startsWith('Showing last valid preview.'));
 assert.equal(await page.locator('#policyPreview').innerHTML(),before,'Invalid edit cleared preview');
 await requirement.fill(original+' **Verification** <script>alert(1)</script>');
 await page.waitForFunction(()=>document.querySelector('#policyPreviewStatus')?.textContent==='Preview up to date');
 assert.equal(await page.locator('#policyPreview #accounts strong').filter({hasText:'Verification'}).count(),1);
 assert.equal(await page.locator('#policyPreview script').count(),0);
 await requirement.fill(original);
 await page.waitForFunction(()=>document.querySelector('#policyPreviewStatus')?.textContent==='Preview up to date');
 assert.deepEqual(JSON.parse((await page.locator('#policyBody').inputValue()).slice('yakult-policy:v1:'.length)),doc);
 await page.screenshot({path:path.join(out,'editor-preview.png')});
 const token=await page.locator('#companyPolicyForm input[name="__RequestVerificationToken"]').inputValue();
 const form={Title:'test',Category:'IT Policy',Body:body,__RequestVerificationToken:token};
 assert.equal((await ctx.request.post(base+'/Admin/CompanyInfo/Policy/Preview',{form:{...form,__RequestVerificationToken:''}})).status(),400);
 const duplicate=structuredClone(doc);duplicate.sections[1].id=duplicate.sections[0].id;
 assert.equal((await ctx.request.post(base+'/Admin/CompanyInfo/Policy/Preview',{form:{...form,Body:'yakult-policy:v1:'+JSON.stringify(duplicate)}})).status(),400);
 const legacy=await ctx.request.post(base+'/Admin/CompanyInfo/Policy/Preview',{form:{...form,Body:'**Markdown**\n\n<script>alert(1)</script> [unsafe](javascript:alert(1))'}});
 assert.equal(legacy.status(),200);assert.ok((await legacy.text()).includes('<strong>Markdown</strong>'));assert.ok(!(await legacy.text()).includes('<script>'));
 const anon=await browser.newContext();
 assert.equal((await anon.request.post(base+'/Admin/CompanyInfo/Policy/Preview',{form})).status(),401);
 const reader=await browser.newContext({extraHTTPHeaders:{'X-Policy-Test-Role':'Employee'}});
 assert.equal((await reader.request.post(base+'/Admin/CompanyInfo/Policy/Preview',{form})).status(),403);
 assert.deepEqual(errors,[],'JavaScript errors');
 fs.writeFileSync(path.join(out,'checks.json'),JSON.stringify({status:'passed',procedures:7,images:9,widths:[1440,768,390,320],checks:['approved instructions and order','image references and intrinsic dimensions','Profile Account round-trip','shared preview','safe Markdown','validation retention','navigation and scroll tracking','search without hiding','paired screenshot layout','modal keyboard and focus','anti-forgery and authorization','no horizontal overflow','no JavaScript errors']},null,2));
 console.log('PASS: policy presentation, editor, security, and desktop/mobile checks');
 } finally {await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});

