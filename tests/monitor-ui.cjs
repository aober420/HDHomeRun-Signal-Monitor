const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict');
class Node {constructor(){this.children=[];this.listeners={};this.style={};this.textContent='';}appendChild(n){this.children.push(n);return n;}replaceChildren(){this.children=[];}setAttribute(){}addEventListener(k,f){this.listeners[k]=f;}getContext(){return {beginPath(){},moveTo(){},lineTo(){},stroke(){}};}}
const nodes=Object.fromEntries(['#signalDevices','#signalStatus','#tunerUrls','#freeGuideForm'].map(k=>[k,new Node()]));const page=new Node();page.querySelector=k=>nodes[k];let nextPoll,data;
const context={document:{hidden:false,querySelector:()=>page,createElement:()=>new Node()},Map,Set,Date,Math,setTimeout:f=>(nextPoll=f,1),clearTimeout:()=>nextPoll=null,ApiClient:{getPluginConfiguration:async()=>({}),getUrl:s=>s,ajax:async()=>data},Dashboard:{}};
const script=fs.readFileSync(require('node:path').join(__dirname,'../src/config.html'),'utf8').match(/<script type="text\/javascript">([\s\S]*?)<\/script>/)[1];vm.runInNewContext(script,context);
const text=n=>n.textContent+' '+n.children.map(text).join(' ');
async function poll(tuner,scan){data={UpdatedAt:new Date().toISOString(),Devices:[{Address:'tuner',Tuners:[tuner],Scan:scan}]};if(nextPoll)await nextPoll();else{page.listeners.pageshow();await new Promise(setImmediate);}return text(nodes['#signalDevices']).replace(/\s+/g,' ');}
(async()=>{
 let output=await poll({Resource:'tuner0',Channel:null,Frequency:533000000,Strength:0,Quality:0,Symbol:0,NetworkRate:null},{InProgress:true,Progress:42,Found:12});
 assert.match(output,/scan in progress/i);assert.match(output,/42%/);assert.match(output,/533\.0 MHz/);assert.match(output,/Signal strength 0%/);assert.doesNotMatch(output,/Idle|Reception errors/);
 output=await poll({Resource:'tuner0',Channel:null,Strength:75,Quality:60,Symbol:100,NetworkRate:null},null);assert.match(output,/Signal strength 75%/);assert.doesNotMatch(output,/Idle/);
 output=await poll({Resource:'tuner0',Channel:null,Strength:null,Quality:null,Symbol:null,NetworkRate:null},{InProgress:true,Progress:null,Found:null});assert.match(output,/not reporting reception measurements/);assert.doesNotMatch(output,/Signal strength 0%|Idle/);
 output=await poll({Resource:'tuner0',Channel:'32.1',Name:'FOX',Strength:80,Quality:65,Symbol:50,NetworkRate:4000000},null);assert.match(output,/32\.1 FOX/);assert.match(output,/Reception errors detected/);
 page.listeners.pagehide();assert.equal(nextPoll,null);console.log('Scan, missing readings, normal playback, and polling cleanup passed.');
})().catch(e=>{console.error(e);process.exitCode=1;});
