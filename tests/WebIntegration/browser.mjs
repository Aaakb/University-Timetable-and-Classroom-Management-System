import path from 'node:path';
import fs from 'node:fs/promises';
import { pathToFileURL } from 'node:url';
import assert from 'node:assert/strict';

// Uses an installed Playwright package; no credentials are written to disk.
const modulePath = process.env.PLAYWRIGHT_MODULE || path.join(path.dirname(process.execPath), '../node_modules/playwright/index.mjs');
const { chromium } = await import(pathToFileURL(modulePath));
const output = path.resolve('.tmp/WebIntegration'); await fs.mkdir(output,{recursive:true});
const profile = await fs.mkdtemp(path.join(output,'browser-profile-'));
const context = await chromium.launchPersistentContext(profile,{ channel:'msedge', headless:true, viewport:{width:1366,height:768}, acceptDownloads:true });
const page = await context.newPage();
const results = [], errors=[]; let ownedBranch;
const check=(name,value=true)=>{assert.ok(value,name);results.push(name);console.log('BROWSER PASS '+name);};
page.on('pageerror',error=>errors.push(error.message));
const base=process.env.PWA_TEST_URL||'http://localhost:5180';
try {
  await page.goto(base);
  await page.locator('#auth-submit').waitFor();
  await page.screenshot({path:path.join(output,'login.png'),fullPage:true});
  await page.locator('[name=userName]').fill(process.env.PWA_TEST_USER);
  await page.locator('[name=password]').fill(process.env.PWA_TEST_PASSWORD);
  await page.locator('#auth-submit').click();
  await page.locator('.stats').waitFor({timeout:30000});
  check('Real browser login and live dashboard');
  await page.evaluate(()=>navigator.serviceWorker.ready);
  check('First installation does not show a spurious update banner',!await page.locator('#update-banner').isVisible());
  await page.screenshot({path:path.join(output,'dashboard.png'),fullPage:true});
  for(const route of ['classrooms','branches','studyyears','sections','subjects','facultymembers','assignments','timeslots','schedules']){
    await page.locator(`nav a[href="#${route}"]`).click();
    await page.locator('#loading').waitFor({state:'hidden',timeout:30000});
    assert.equal(await page.locator('#page-error').isVisible(),false,route+' load error');
    assert.ok(await page.locator('#table-host table').count()||await page.locator('.empty').count());
    await page.locator('#add').click();await page.locator('#editor').waitFor();
    assert.ok(await page.locator('#editor-fields input, #editor-fields select').count());
    await page.locator('#cancel-editor').click();
    check(route+' page and editor');
  }
  await page.locator('#timetable-view').click();
  check('Weekly timetable renders',await page.locator('.timetable').count()===1);
  await page.screenshot({path:path.join(output,'timetable.png'),fullPage:true});
  const downloadPromise=page.waitForEvent('download');await page.locator('#export').click();
  const download=await downloadPromise;check('Browser PDF download',download.suggestedFilename().endsWith('.pdf'));
  for(const [width,height] of [[1920,1080],[1366,768],[1280,720],[1024,768],[768,1024],[390,844],[360,800]]){
    await page.setViewportSize({width,height});
    const overflow=await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1);
    check(`No page overflow at ${width}x${height}`,!overflow);
    if(width===390)await page.screenshot({path:path.join(output,'mobile-timetable.png'),fullPage:false,animations:'disabled'});
  }
  await page.locator('#menu').click();await page.locator('nav a[href="#branches"]').click();
  await page.locator('#loading').waitFor({state:'hidden'});
  check('Mobile navigation closes after selection',!await page.locator('body').evaluate(x=>x.classList.contains('menu-open')));
  const name='UI'+Date.now();
  await page.locator('#add').click();await page.locator('#editor [name=branchName]').fill(name);await page.locator('#save').click();
  await page.locator('#editor').waitFor({state:'hidden'});await page.locator('#loading').waitFor({state:'hidden'});
  const branchResponse=await page.request.get(base+'/api/branches');const branchData=await branchResponse.json();
  ownedBranch=branchData.items.find(x=>x.data.branchName===name);assert.ok(ownedBranch);
  await page.locator('#search').fill(name);assert.equal(await page.locator('tbody tr').count(),1);
  await page.locator('button[data-edit]').click();await page.locator('#editor [name=branchName]').fill(name+'E');await page.locator('#save').click();
  await page.locator('#editor').waitFor({state:'hidden'});await page.locator('#loading').waitFor({state:'hidden'});
  check('Mobile create/search/edit workflow',await page.locator('tbody').innerText().then(x=>x.includes(name+'E')));
  await page.locator('button[data-delete]').click();await page.locator('#confirmation button[value=yes]').click();
  await page.locator('.empty').waitFor();ownedBranch=null;check('Confirmed browser deletion');
  await page.setViewportSize({width:390,height:844});await page.locator('#menu').click();await page.locator('nav a[href="#dashboard"]').click();
  await page.locator('.stats').waitFor();await page.screenshot({path:path.join(output,'mobile-dashboard.png'),fullPage:true,animations:'disabled'});
  check('Mobile dashboard has no page overflow',await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1));
  await page.evaluate(()=>navigator.serviceWorker.ready);
  const cached=await page.evaluate(async()=>{const names=await caches.keys();return(await Promise.all(names.map(async n=>(await(await caches.open(n)).keys()).map(r=>r.url)))).flat();});
  check('Service worker caches static assets and no API data',cached.length>5&&!cached.some(x=>new URL(x).pathname.startsWith('/api/')));
  const cdp=await context.newCDPSession(page);const install=await cdp.send('Page.getInstallabilityErrors');
  check('Chromium installability checks',install.installabilityErrors.length===0);
  await context.setOffline(true);await page.reload();await page.getByRole('heading',{name:'الخادم غير متاح الآن'}).waitFor();
  check('Offline navigation displays honest fallback');
  await context.setOffline(false);await page.goto(base);await page.locator('.stats').waitFor();
  check('Online recovery retains valid server session');
  await page.locator('#logout').click();await page.locator('#auth').waitFor();
  check('Logout removes sensitive rendered data',await page.locator('#dashboard').innerHTML()==='');
  check('No JavaScript runtime exceptions',errors.length===0);
} finally {
  if(ownedBranch){
    await context.setOffline(false);
    const token=await(await page.request.get(base+'/api/auth/csrf')).json();
    const row=await(await page.request.get(base+`/api/branches/${ownedBranch.data.branchID}`)).json();
    const deleted=await page.request.delete(base+`/api/branches/${ownedBranch.data.branchID}`,{headers:{'X-CSRF-TOKEN':token.token,'If-Match':`"${row.version}"`}});
    assert.ok(deleted.ok(),'Browser test-owned record cleanup');
  }
  await fs.writeFile(path.join(output,'browser-results.json'),JSON.stringify({results,errors},null,2));
  await context.clearCookies(); await context.close();
  const resolved = await fs.realpath(profile), parent = await fs.realpath(output);
  assert.equal(path.dirname(resolved),parent); assert.ok(path.basename(resolved).startsWith('browser-profile-'));
  await fs.rm(resolved,{recursive:true,force:true});
}
