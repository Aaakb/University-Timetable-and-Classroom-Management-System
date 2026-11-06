import assert from 'node:assert/strict';
import {createServer} from 'node:http';
import {readFile} from 'node:fs/promises';
import {resolve, extname} from 'node:path';
const root=resolve('UniversityTimetable.API/wwwroot');
const server=createServer(async(req,res)=>{try {const path=resolve(root,'.'+decodeURIComponent(req.url.split('?')[0]==='/'?'/index.html':req.url.split('?')[0]));if(!path.startsWith(root))throw Error();const data=await readFile(path);res.setHeader('Content-Type',({'.js':'text/javascript','.css':'text/css','.html':'text/html','.json':'application/json','.webmanifest':'application/manifest+json','.svg':'image/svg+xml'})[extname(path)]||'application/octet-stream');res.end(data);}catch{res.statusCode=404;res.end();}});
await new Promise(r=>server.listen(0,'127.0.0.1',r));
const {chromium}=await import('file:///C:/Users/aloli/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright/index.mjs');
const browser=await chromium.launch({channel:'msedge',headless:true});
try{
 const page=await browser.newPage();
 await page.route('**/api/**',async r=>{const path=new URL(r.request().url()).pathname;const data=path.endsWith('/me')?{fullName:'Audit'}:path.endsWith('/csrf')?{token:'audit'}:path.endsWith('/overview')?{classrooms:0,subjects:0,facultyMembers:0,sections:0,schedules:0,assignments:0,unassignedSubjects:0}:{items:[],total:0,page:1,pageSize:200};await r.fulfill({contentType:'application/json',body:JSON.stringify(data)});});
 await page.goto(`http://127.0.0.1:${server.address().port}/#dashboard`);
 await page.locator('.stats').waitFor();
 await page.waitForTimeout(1200);
 await page.locator('nav a[href="#branches"]').click();
 await page.locator('#loading').waitFor({state:'hidden'});
 await page.locator('#branch-form [name=branchName]').fill('Audit');
 await page.locator('#branch-form button[value=add]').click();
 await page.waitForTimeout(1600);
 assert.equal(await page.locator('#management').isVisible(),true,'Fast save must leave management visible');assert.equal(await page.locator('#page-error').isVisible(),false);console.log('PASS fast save during page transition leaves the page visible');
}finally{await browser.close();server.close();}
