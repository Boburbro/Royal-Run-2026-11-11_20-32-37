// The controller HTML page. A phone opens it at http://IP:port.
// The text contains no double quotes, so the string can be stored as @"..." without escaping.
public static class ControllerPage
{
    public const string Html = @"<!DOCTYPE html>
<html lang='en'>
<head>
<meta charset='utf-8'>
<meta name='viewport' content='width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no, viewport-fit=cover'>
<meta name='theme-color' content='#150d29'>
<title>Royal Run - controller</title>
<style>
:root{--c:#9a8cc0;--gold:#ffd54f;--bg1:#241545;--bg2:#0e0919}
*{box-sizing:border-box;-webkit-tap-highlight-color:transparent;-webkit-user-select:none;user-select:none;-webkit-touch-callout:none}
html,body{margin:0;height:100%;background:var(--bg2);color:#fff;font-family:system-ui,-apple-system,'Segoe UI',Roboto,Arial,sans-serif;overscroll-behavior:none;overflow:hidden}
body{background:radial-gradient(120% 80% at 50% 0%,var(--bg1),var(--bg2))}
.screen{display:none;position:fixed;inset:0;flex-direction:column;align-items:center;padding:calc(env(safe-area-inset-top,0px) + 16px) 16px calc(env(safe-area-inset-bottom,0px) + 16px)}
h1{margin:4px 0 0;font-size:36px;letter-spacing:3px;color:var(--gold);text-shadow:0 3px 0 #6d4c00}
.crown{font-size:58px;line-height:1;margin-top:6px}
.sub{opacity:.75;margin:6px 0 18px;text-align:center;line-height:1.35}
input{width:100%;max-width:340px;font-size:22px;padding:15px 16px;border-radius:16px;border:2px solid #4a4266;background:#1d1636;color:#fff;text-align:center;-webkit-user-select:text;user-select:text;outline:none}
input:focus{border-color:var(--gold)}
button{font-size:22px;font-weight:800;padding:16px 22px;border:0;border-radius:18px;color:#1a1030;background:linear-gradient(#ffe27a,#ffc107);width:100%;max-width:340px;box-shadow:0 5px 0 #a67c00}
button:active{transform:translateY(3px);box-shadow:0 2px 0 #a67c00}
button:disabled{opacity:.35;box-shadow:none}
button.ghost{background:#ffffff18;color:#fff;box-shadow:none;border:2px solid #ffffff30}
#join,#wait,#count,#results{justify-content:center;gap:12px}
.pill{display:inline-flex;align-items:center;gap:8px;padding:7px 14px;border-radius:999px;background:#ffffff14;font-size:15px}
.dot{width:11px;height:11px;border-radius:50%;background:#e53935;box-shadow:0 0 8px #e53935}
.dot.ok{background:#43d65f;box-shadow:0 0 8px #43d65f}
.hint{font-size:14px;opacity:.6;text-align:center;max-width:320px;line-height:1.4;min-height:20px}
.spin{width:54px;height:54px;border-radius:50%;border:6px solid #ffffff25;border-top-color:var(--gold);animation:sp 1s linear infinite}
@keyframes sp{to{transform:rotate(360deg)}}
#head{display:flex;align-items:center;gap:12px;width:100%;max-width:420px;padding:10px 14px;border-radius:18px;background:#ffffff12;border-left:8px solid var(--c)}
#head .badge{width:42px;height:42px;border-radius:50%;background:var(--c);display:flex;align-items:center;justify-content:center;font-weight:900;font-size:22px;color:#1a1030}
#head b{font-size:21px;display:block}
#head span{font-size:13px;opacity:.65}
#cards{flex:1;width:100%;max-width:420px;overflow-y:auto;display:flex;flex-direction:column;gap:10px;margin:12px 0;padding:2px 2px 6px;-webkit-overflow-scrolling:touch;touch-action:pan-y}
.card{display:flex;align-items:center;gap:12px;padding:12px 14px;border-radius:18px;background:#ffffff10;border:3px solid transparent;text-align:left;position:relative;flex:0 0 auto}
.card .face img{width:100%;height:100%;object-fit:cover;display:block}
.card .face{flex:0 0 66px;height:110px;border-radius:14px;border:3px solid #ffffff30;display:flex;align-items:center;justify-content:center;overflow:hidden}
.card .face svg{width:46px;height:68px}
#others .op img{width:26px;height:43px;object-fit:cover;border-radius:6px;margin-right:6px;flex:0 0 26px}
#others .op svg{width:22px;height:33px;margin-right:6px;vertical-align:middle;flex:0 0 22px}
#others .op span{display:flex;align-items:center}
#others .op i{display:flex;gap:4px}
#others .op i em{font-style:normal;padding:3px 8px;border-radius:8px;color:#fff;text-shadow:0 1px 2px #000a;font-size:12px}
.card b{display:block;font-size:20px}
.card span{display:block;font-size:14px;opacity:.75;line-height:1.25}
.card.sel{border-color:var(--c);background:#ffffff1f;box-shadow:0 0 18px var(--c)}
.card.taken{opacity:.3}
#others{width:100%;max-width:420px;margin:0 0 6px}
#others .op{display:flex;justify-content:space-between;gap:8px;padding:6px 10px;border-radius:10px;background:#ffffff10;margin-bottom:4px;font-size:13px;border-left:5px solid #888}
#others .op i{opacity:.7;font-style:normal;text-align:right}
#countInfo{margin-top:10px;font-size:18px;font-weight:800;color:var(--gold);text-align:center}
.vote{width:100%;max-width:420px;margin:0 0 6px}
.vote small{display:block;opacity:.7;font-size:12px;margin:2px 0 4px;text-align:left}
.chips{display:flex;flex-wrap:wrap;gap:6px}
.chip{padding:9px 12px;border-radius:14px;border:3px solid transparent;font-size:14px;font-weight:800;color:#fff;text-shadow:0 1px 3px #000c;opacity:.65;min-width:74px;text-align:center}
.chip.on{border-color:var(--gold);opacity:1;box-shadow:0 0 12px #ffd54f88}
.card .tag{position:absolute;right:12px;top:10px;font-size:12px;font-weight:800;padding:3px 8px;border-radius:8px;background:#00000066;display:none}
.card.taken .tag,.card.sel .tag{display:block}
#status{min-height:22px;opacity:.85;margin:0 0 10px;text-align:center;font-size:15px}
#countNum{font-size:170px;font-weight:900;color:var(--gold);text-shadow:0 7px 0 #6d4c00;animation:pop 1s ease-out infinite}
@keyframes pop{0%{transform:scale(1.5);opacity:.2}35%{transform:scale(1);opacity:1}100%{transform:scale(.9);opacity:.9}}
#play{padding:0;touch-action:none;background:radial-gradient(circle at 50% 65%,#33265a,#0e0919 70%)}
#hud{position:absolute;top:calc(env(safe-area-inset-top,0px) + 12px);left:14px;right:14px;pointer-events:none}
#hud .row{display:flex;justify-content:space-between;align-items:flex-end;font-size:30px;font-weight:900}
#hud small{display:block;font-size:11px;font-weight:700;opacity:.55;letter-spacing:2px}
#bar{height:12px;border-radius:8px;background:#ffffff20;margin-top:8px;overflow:hidden}
#barFill{height:100%;width:100%;border-radius:8px;background:#43d65f;transition:width .25s linear,background .3s}
#hint{position:absolute;bottom:20%;left:0;right:0;text-align:center;pointer-events:none}
#hint .ring{width:120px;height:120px;border-radius:50%;border:4px dashed #ffffff55;margin:0 auto 14px;animation:pulse 1.6s ease-in-out infinite}
@keyframes pulse{50%{transform:scale(1.12);opacity:.5}}
#hint div:last-child{opacity:.6;font-size:17px}
#over{position:absolute;top:36%;left:0;right:0;text-align:center;font-size:36px;font-weight:900;color:#ff8a80;display:none;pointer-events:none;text-shadow:0 3px 0 #000a}
#paused{position:absolute;inset:0;display:none;align-items:center;justify-content:center;flex-direction:column;background:#000a;font-size:44px;font-weight:900;color:var(--gold);pointer-events:none;text-shadow:0 4px 0 #000}
#paused small{font-size:16px;font-weight:600;color:#fff;opacity:.7;margin-top:8px}
#flash{position:absolute;inset:0;pointer-events:none;box-shadow:inset 0 0 90px 24px #ff1744;opacity:0;transition:opacity .35s}
#base,#knob{position:absolute;border-radius:50%;display:none;pointer-events:none}
#base{width:140px;height:140px;margin:-70px 0 0 -70px;border:4px solid var(--c);background:#ffffff14}
#knob{width:64px;height:64px;margin:-32px 0 0 -32px;background:var(--c);box-shadow:0 4px 14px #0008}
#medal{font-size:84px;line-height:1}
#rank{font-size:34px;font-weight:900;color:var(--gold)}
#list{width:100%;max-width:340px;margin:8px 0 14px}
.row2{display:flex;justify-content:space-between;align-items:center;padding:11px 14px;border-radius:14px;background:#ffffff12;margin-bottom:8px;font-size:20px;border-left:7px solid #888}
.row2.me{outline:3px solid var(--gold)}
#banner{position:fixed;left:0;right:0;top:0;padding:calc(env(safe-area-inset-top,0px) + 8px) 12px 8px;background:#b71c1c;text-align:center;display:none;z-index:9;font-weight:700}
</style>
</head>
<body>
<div id='banner'></div>
 
<div id='join' class='screen'>
  <div class='crown'>👑</div>
  <h1>ROYAL RUN</h1>
  <p class='sub'>This is your controller.<br>Enter a name and jump in!</p>
  <input id='nameIn' maxlength='12' placeholder='Your name' autocomplete='off'>
  <button id='joinBtn' disabled>Join the game</button>
  <div class='pill'><span class='dot' id='conDot'></span><span id='conText'>Connecting to the game...</span></div>
  <p class='hint' id='conHint'></p>
</div>
 
<div id='wait' class='screen'>
  <div class='spin'></div>
  <h1 style='font-size:26px'>Please wait</h1>
  <p class='sub' id='waitMsg'>The game is not ready yet.</p>
</div>
 
<div id='lobby' class='screen'>
  <div id='head'><div class='badge' id='slotNum'>1</div><div><b id='who'>Player</b><span>Pick a hero and tap READY</span></div></div>
  <div id='cards'></div>
  <div class='vote'><small>VOTE: ZONE</small><div class='chips' id='zoneRow'></div></div>
  <div class='vote'><small>VOTE: TIME OF DAY</small><div class='chips' id='timeRow'></div></div>
  <div id='others'></div>
  <p id='status'></p>
  <button id='readyBtn' disabled>READY</button>
</div>
 
<div id='count' class='screen'>
  <p class='sub'>Get ready!</p>
  <div id='countNum'>3</div>
  <div id='countInfo'></div>
</div>
 
<div id='play' class='screen'>
  <div id='hud'>
    <div class='row'>
      <div><small>STAGE</small><span id='hTime'>1</span></div>
      <div style='text-align:right'><small>SCORE</small><span id='hScore'>0</span></div>
    </div>
    <div id='bar'><div id='barFill'></div></div>
  </div>
  <div id='hint'><div class='ring'></div><div>Drag and flick: left / right = lane, up = jump, down = slide</div></div>
  <div id='over'>CRASHED!</div>
  <div id='paused'>PAUSED<small>The host paused the game</small></div>
  <div id='flash'></div>
  <div id='base'></div><div id='knob'></div>
</div>
 
<div id='results' class='screen'>
  <div id='medal'>🏆</div>
  <div id='rank'>1st place</div>
  <div id='list'></div>
  <button id='againBtn'>Play again</button>
</div>
 
<script>
(function(){
var $=function(i){return document.getElementById(i)};
var SCREENS=['join','wait','lobby','count','play','results'];
var cid=null,savedName='';
try{cid=localStorage.getItem('rr_cid');savedName=localStorage.getItem('rr_name')||''}catch(e){}
if(!cid){cid=Math.random().toString(36).slice(2)+Date.now().toString(36);try{localStorage.setItem('rr_cid',cid)}catch(e){}}
 
var ws=null,wantJoin=false,noRetry=false,current='',tries=0;
var me={slot:0,chars:[],char:-1,ready:false,color:'#9a8cc0',imgs:[],lastLobby:null};
var COLORS=['#ef5350','#42a5f5','#66bb6a'];
 
function show(id){
  current=id;
  $('paused').style.display='none';
  SCREENS.forEach(function(s){$(s).style.display=(s===id?'flex':'none')});
}
function banner(text){var b=$('banner');if(text){b.textContent=text;b.style.display='block'}else{b.style.display='none'}}
function send(o){if(ws&&ws.readyState===1){ws.send(JSON.stringify(o));return true}return false}
function setCon(ok,text,hint){
  $('conDot').className='dot'+(ok?' ok':'');
  $('conText').textContent=text;
  $('conHint').textContent=hint||'';
  $('joinBtn').disabled=!ok;
}
 
function connect(){
  if(noRetry)return;
  tries++;
  var failTimer=setTimeout(function(){
    if(ws&&ws.readyState!==1){
      setCon(false,'No connection to the game','Make sure the phone is on the same Wi-Fi network as the computer and the address starts with http://');
    }
  },4000);
  try{ws=new WebSocket('ws://'+location.host+'/ws')}catch(e){setTimeout(connect,1500);return}
  ws.onopen=function(){
    clearTimeout(failTimer);
    tries=0;banner('');
    setCon(true,'Connected to the game','');
    if(wantJoin){send({t:'join',cid:cid,name:savedName})}
  };
  ws.onmessage=function(ev){var m;try{m=JSON.parse(ev.data)}catch(e){return}handle(m)};
  ws.onclose=function(){
    clearTimeout(failTimer);
    endTouch();
    if(noRetry)return;
    setCon(false,'Connection lost','Reconnecting...');
    if(current!=='join')banner('Connection lost. Reconnecting...');
    setTimeout(connect,1500);
  };
}
 
function handle(m){
  switch(m.t){
    case 'joined':
      me.slot=m.slot;me.chars=m.chars||[];me.color=m.color;me.zones=m.zones||[];me.times=m.times||[];
      document.documentElement.style.setProperty('--c',m.color);
      $('who').textContent=m.name;
      $('slotNum').textContent=(m.slot+1);
      buildCards();buildVotes();
      if(current==='join'||current==='wait'||current==='')show('lobby');
      break;
    case 'img':
      me.imgs=me.imgs||[];me.imgs[m.i]=m.d;
      buildCards();
      if(me.lastLobby)updateLobby(me.lastLobby);
      break;
    case 'wait':
      wantJoin=true;
      $('waitMsg').textContent=m.msg||'The game is not ready yet.';
      show('wait');
      setTimeout(function(){if(current==='wait')send({t:'join',cid:cid,name:savedName})},2000);
      break;
    case 'full':
      noRetry=true;wantJoin=false;
      banner(m.msg||'You cannot join this game');
      break;
    case 'lobby':
      me.char=m.char;me.ready=m.ready;me.lastLobby=m;
      updateLobby(m);
      show('lobby');
      break;
    case 'count':
      $('countInfo').textContent=m.info||'';
      $('countNum').textContent=(m.n>0?m.n:'GO!');
      $('countNum').style.animation='none';void $('countNum').offsetWidth;$('countNum').style.animation='';
      show('count');
      break;
    case 'play':
      if(current!=='play'){show('play');$('hint').style.display='block'}
      $('hTime').textContent=m.stage;
      $('hScore').textContent=m.score;
      var r=Math.max(0,Math.min(1,m.prog));
      var f=$('barFill');f.style.width=(r*100)+'%';
      f.style.background='#ffc107';
      $('over').style.display=m.over?'block':'none';
      break;
    case 'pause':
      $('paused').style.display=(m.on&&current==='play')?'flex':'none';
      break;
    case 'sfx':
      playSfx(m.n,m.p);
      break;
    case 'hit':
      if(navigator.vibrate){navigator.vibrate(150)}
      var fl=$('flash');fl.style.opacity=1;setTimeout(function(){fl.style.opacity=0},180);
      break;
    case 'results':
      showResults(m);
      break;
  }
}
 
function heroImg(i){return(me.imgs&&me.imgs[i])?'<img src=\'data:image/png;base64,'+me.imgs[i]+'\'>':''}
function avatar(l){
  if(!l)return '';
  var cap=(l.hd===2)?l.hr:l.ht;
  var s='<svg viewBox=\'0 0 60 90\'>';
  s+='<rect x=\'16\' y=\'60\' width=\'13\' height=\'22\' fill=\''+l.pn+'\'/><rect x=\'31\' y=\'60\' width=\'13\' height=\'22\' fill=\''+l.pn+'\'/>';
  s+='<rect x=\'14\' y=\'80\' width=\'16\' height=\'7\' rx=\'3\' fill=\''+l.sh+'\'/><rect x=\'30\' y=\'80\' width=\'16\' height=\'7\' rx=\'3\' fill=\''+l.sh+'\'/>';
  s+='<rect x=\'8\' y=\'34\' width=\'8\' height=\'26\' rx=\'4\' fill=\''+l.tp+'\'/><rect x=\'44\' y=\'34\' width=\'8\' height=\'26\' rx=\'4\' fill=\''+l.tp+'\'/>';
  s+='<circle cx=\'12\' cy=\'62\' r=\'4.5\' fill=\''+l.sk+'\'/><circle cx=\'48\' cy=\'62\' r=\'4.5\' fill=\''+l.sk+'\'/>';
  s+='<rect x=\'15\' y=\'32\' width=\'30\' height=\'32\' rx=\'6\' fill=\''+l.tp+'\'/><rect x=\'15\' y=\'58\' width=\'30\' height=\'4\' fill=\''+l.ac+'\'/>';
  if(l.pt)s+='<circle cx=\'10\' cy=\'16\' r=\'6\' fill=\''+l.hr+'\'/>';
  s+='<circle cx=\'30\' cy=\'16\' r=\'15\' fill=\''+cap+'\'/><circle cx=\'30\' cy=\'21\' r=\'13\' fill=\''+l.sk+'\'/>';
  if(l.hd===2)s+='<rect x=\'17\' y=\'13\' width=\'26\' height=\'4\' fill=\''+l.ht+'\'/>';
  if(l.hd===1)s+='<rect x=\'32\' y=\'12\' width=\'18\' height=\'4\' rx=\'2\' fill=\''+l.ht+'\'/>';
  if(l.gl)s+='<rect x=\'19\' y=\'19\' width=\'9\' height=\'6\' rx=\'2\' fill=\'#111\'/><rect x=\'32\' y=\'19\' width=\'9\' height=\'6\' rx=\'2\' fill=\'#111\'/>';
  return s+'</svg>';
}
function grad(o,vert){return 'linear-gradient('+(vert?'to bottom':'to right')+','+o.a+','+o.g+')'}

var FACES=['👑','🛡️','🏹','⚗️','🔥'];
function buildCards(){
  var box=$('cards');box.innerHTML='';
  me.chars.forEach(function(c,i){
    var d=document.createElement('div');
    d.className='card';d.id='card'+i;
    d.innerHTML='<div class=\'face\'></div><div><b></b><span></span></div><div class=\'tag\'></div>';
    d.querySelector('.face').style.background=c.c+'55';
    d.querySelector('.face').innerHTML=heroImg(i)||(c.l?avatar(c.l):FACES[i%FACES.length]);
    d.querySelector('b').textContent=c.n;
    d.querySelector('span').textContent=c.d;
    d.addEventListener('click',function(){send({t:'char',id:i})});
    box.appendChild(d);
  });
}
 
var ZONES=['Samarkand','Bukhara','Mountains','Tashkent','Mix'],TIMES=['Day','Sunset','Evening','Night','Auto'];
function buildVotes(){
  [['zoneRow',me.zones,'z'],['timeRow',me.times,'t']].forEach(function(r){
    var box=$(r[0]);box.innerHTML='';
    r[1].forEach(function(n,i){
      var c=document.createElement('div');c.className='chip';c.textContent=n;c.id=r[2]+'chip'+i;
      c.style.background=grad(n,r[2]==='t');c.textContent=(n.e?n.e+' ':'')+n.n;
      c.addEventListener('click',function(){send({t:'vote',k:r[2],id:i})});
      box.appendChild(c);
    });
  });
}
function updateVotes(m){
  for(var i=0;i<5;i++){
    var z=$('zchip'+i),t=$('tchip'+i);
    if(z)z.className='chip'+(m.vz===i?' on':'');
    if(t)t.className='chip'+(m.vt===i?' on':'');
  }
}
function updateOthers(m){
  var box=$('others');box.innerHTML='';
  (m.others||[]).forEach(function(o){
    var d=document.createElement('div');d.className='op';d.style.borderLeftColor=COLORS[o.s%3];
    var a=document.createElement('span');
    var ch=me.chars[o.hi];
    a.innerHTML=heroImg(o.hi)||(ch&&ch.l?avatar(ch.l):'');
    a.appendChild(document.createTextNode(o.n+(o.r?' ✔':'')));
    var b=document.createElement('i');
    var zz=me.zones[o.zi],tt=me.times[o.ti];
    if(zz){var e1=document.createElement('em');e1.textContent=zz.n;e1.style.background=grad(zz,false);b.appendChild(e1)}
    if(tt){var e2=document.createElement('em');e2.textContent=tt.n;e2.style.background=grad(tt,true);b.appendChild(e2)}
    d.appendChild(a);d.appendChild(b);box.appendChild(d);
  });
}
function updateLobby(m){
  updateVotes(m);updateOthers(m);
  me.chars.forEach(function(c,i){
    var el=$('card'+i);if(!el)return;
    var mine=(i===me.char),tk=(m.taken&&m.taken[i]&&!mine);
    el.className='card'+(mine?' sel':'')+(tk?' taken':'');
    el.querySelector('.tag').textContent=mine?'YOURS':'TAKEN';
  });
  var rb=$('readyBtn');
  rb.disabled=(me.char<0);
  rb.textContent=me.ready?'Not ready':'READY';
  rb.className=me.ready?'ghost':'';
  var st=$('status');
  if(me.char<0)st.textContent='Tap a hero to choose';
  else if(!me.ready)st.textContent='All set? Tap READY';
  else st.textContent='Waiting for the others: '+m.readyCount+' of '+m.players+' ready';
}
 
function showResults(m){
  var medals=['🥇','🥈','🥉'];
  $('medal').textContent=medals[m.rank-1]||'🏁';
  var sfx=['th','st','nd','rd'];var rk=m.rank;var ord=(rk%100>=11&&rk%100<=13)?'th':(sfx[rk%10<4?rk%10:0]);$('rank').textContent=rk+ord+' place';
  var l=$('list');l.innerHTML='';
  (m.list||[]).forEach(function(r,i){
    var d=document.createElement('div');
    d.className='row2'+(r.slot===me.slot?' me':'');
    d.style.borderLeftColor=COLORS[r.slot%3];
    var a=document.createElement('span');a.textContent=(i+1)+'. '+r.name;
    var b=document.createElement('b');b.textContent=r.score;
    d.appendChild(a);d.appendChild(b);l.appendChild(d);
  });
  $('againBtn').disabled=false;
  show('results');
}
 
$('joinBtn').addEventListener('click',function(){
  var n=$('nameIn').value.trim();
  savedName=n;
  try{localStorage.setItem('rr_name',n)}catch(e){}
  wantJoin=true;
  send({t:'join',cid:cid,name:n});
});
$('nameIn').addEventListener('keydown',function(e){if(e.key==='Enter'){$('joinBtn').click()}});
$('readyBtn').addEventListener('click',function(){send({t:'ready',v:!me.ready})});
$('againBtn').addEventListener('click',function(){$('againBtn').disabled=true;send({t:'again'})});
 
/* ---------- floating joystick ---------- */
var area=$('play'),base=$('base'),knob=$('knob');
var fired=false,R=70,SW=32,DEAD=0.12,active=-1,ox=0,oy=0,vx=0,vy=0,lastSend=0,pending=null;
 
function sendNow(){pending=null;lastSend=Date.now();send({t:'move',x:+vx.toFixed(3),y:+vy.toFixed(3)})}
function sendMove(force){
  var n=Date.now();
  if(force||n-lastSend>=33){sendNow()}
  else if(pending===null){pending=setTimeout(sendNow,33-(n-lastSend))}
}
function place(el,x,y){el.style.left=x+'px';el.style.top=y+'px'}
function update(e){
  var dx=e.clientX-ox,dy=e.clientY-oy,d=Math.sqrt(dx*dx+dy*dy);
  if(fired){place(knob,ox+Math.max(-R,Math.min(R,dx)),oy+Math.max(-R,Math.min(R,dy)));return} // one swipe per touch
  if(d>=SW){
    var dir=Math.abs(dx)>Math.abs(dy)?(dx>0?'r':'l'):(dy<0?'u':'d');
    send({t:'swipe',d:dir});
    if(navigator.vibrate){navigator.vibrate(8)}
    fired=true;
    return;
  }
  place(knob,ox+dx,oy+dy);
}
function endTouch(){
  if(active===-1)return;
  active=-1;vx=0;vy=0;
  if(pending!==null){clearTimeout(pending);pending=null}
  base.style.display='none';knob.style.display='none';
  send({t:'move',x:0,y:0});
}
area.addEventListener('pointerdown',function(e){
  if(active!==-1)return;
  active=e.pointerId;
  fired=false;
  try{area.setPointerCapture(e.pointerId)}catch(x){}
  ox=e.clientX;oy=e.clientY;
  place(base,ox,oy);place(knob,ox,oy);
  base.style.display='block';knob.style.display='block';
  $('hint').style.display='none';
  update(e);e.preventDefault();
});
area.addEventListener('pointermove',function(e){if(e.pointerId===active){update(e);e.preventDefault()}});
area.addEventListener('pointerup',function(e){if(e.pointerId===active)endTouch()});
area.addEventListener('pointercancel',function(e){if(e.pointerId===active)endTouch()});
document.addEventListener('visibilitychange',function(){if(document.hidden)endTouch()});
document.addEventListener('contextmenu',function(e){e.preventDefault()});
// block page scrolling only during the game - in the lobby the hero list must scroll
document.addEventListener('touchmove',function(e){if(current==='play')e.preventDefault()},{passive:false});
 

/* ---------- sound on the phone ---------- */
var AC=null;
function unlock(){try{if(!AC){var C=window.AudioContext||window.webkitAudioContext;if(C)AC=new C()}if(AC&&AC.state==='suspended')AC.resume()}catch(e){}}
document.addEventListener('pointerdown',unlock,true);
document.addEventListener('touchstart',unlock,true);
function tone(f0,f1,dur,vol,type,delay){
  if(!AC)return;var t=AC.currentTime+(delay||0);
  var o=AC.createOscillator(),g=AC.createGain();
  o.type=type||'sine';o.frequency.setValueAtTime(f0,t);
  if(f1&&f1!==f0)o.frequency.exponentialRampToValueAtTime(f1,t+dur);
  g.gain.setValueAtTime(vol,t);g.gain.exponentialRampToValueAtTime(0.0001,t+dur);
  o.connect(g);g.connect(AC.destination);o.start(t);o.stop(t+dur+0.02);
}
function noise(dur,vol,cut,delay){
  if(!AC)return;var t=AC.currentTime+(delay||0),n=Math.floor(AC.sampleRate*dur);
  var b=AC.createBuffer(1,n,AC.sampleRate),d=b.getChannelData(0);
  for(var i=0;i<n;i++)d[i]=(Math.random()*2-1)*(1-i/n);
  var s=AC.createBufferSource();s.buffer=b;
  var f=AC.createBiquadFilter();f.type='lowpass';f.frequency.value=cut;
  var g=AC.createGain();g.gain.value=vol;
  s.connect(f);f.connect(g);g.connect(AC.destination);s.start(t);
}
function playSfx(n,p){
  if(!AC)return;p=p||1;
  switch(n){
    case 'coin':tone(988*p,988*p,0.08,0.25,'sine');tone(1319*p,1319*p,0.25,0.25,'sine',0.07);break;
    case 'jump':tone(250,950,0.28,0.25,'sine');break;
    case 'slide':noise(0.4,0.5,1800);break;
    case 'lane':noise(0.12,0.4,2500);break;
    case 'hit':tone(110,30,0.5,0.6,'sine');noise(0.55,0.6,900);break;
    case 'level':[523,659,784,1047].forEach(function(f,i){tone(f,f,0.3,0.2,'sine',i*0.12)});break;
    case 'revive':[392,523,659,784].forEach(function(f,i){tone(f,f,0.3,0.2,'sine',i*0.1)});break;
  }
}

/* ---------- start ---------- */
$('nameIn').value=savedName;
show('join');
connect();
})();
</script>
</body>
</html>
";
}
