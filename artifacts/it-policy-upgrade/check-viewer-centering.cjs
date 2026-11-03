const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const os=require('node:os');
const {chromium}=require(path.join(os.tmpdir(),'yakult-learning-qa/node_modules/playwright'));
(async()=>{
 const browser=await chromium.launch({headless:true,channel:'msedge'});
 try{
  const page=await browser.newPage();const results=[];
  for(const [width,height] of [[1440,1000],[390,844],[320,568]]){
   await page.setViewportSize({width,height});
   await page.goto('http://192.168.100.186:7016/company-policies/it-policy---employee-guidelines');
   if(process.argv.includes('--local'))await page.addStyleTag({content:fs.readFileSync('Yakult.SystemsPortal/wwwroot/css/company-policy-document.css','utf8')});
   for(const index of [0,2,8]){
    const link=page.locator('[data-policy-image]').nth(index);await link.click();
    await page.locator('dialog img').evaluate(image=>image.decode());
    const box=await page.locator('dialog').evaluate(dialog=>{const r=dialog.getBoundingClientRect();return{x:r.x,y:r.y,width:r.width,height:r.height,viewportWidth:innerWidth,viewportHeight:innerHeight}});
    assert.ok(Math.abs(box.x+box.width/2-width/2)<2,JSON.stringify(box));
    assert.ok(Math.abs(box.y+box.height/2-height/2)<2,JSON.stringify(box));
    assert.ok(box.x>=0&&box.y>=0&&box.x+box.width<=width+1&&box.y+box.height<=height+1,JSON.stringify(box));
    if(index===2)await page.screenshot({path:`artifacts/it-policy-upgrade/viewer-centered-${width}${process.argv.includes('--local')?'-local':''}.png`});
    await page.keyboard.press('Escape');assert.ok(await link.evaluate(el=>el===document.activeElement));
    results.push({image:index+1,...box});
   }
  }
  console.log(JSON.stringify(results));
 }finally{await browser.close()}
})().catch(error=>{console.error(error);process.exitCode=1});
