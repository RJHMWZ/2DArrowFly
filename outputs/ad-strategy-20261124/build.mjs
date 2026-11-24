import fs from 'node:fs/promises';
import { Workbook, SpreadsheetFile } from '@oai/artifact-tool';

const out = new URL('.', import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, '$1');
const root = new URL('../../', import.meta.url);
const policyText = await fs.readFile(new URL('Assets/HueRelay/Resources/HueRelay/Data/AdPolicy.asset',root),'utf8');
const powerText = await fs.readFile(new URL('Assets/HueRelay/Resources/HueRelay/Data/PowerUps.asset',root),'utf8');
function read(text,key) { const v=text.match(new RegExp(`^  ${key}: (.*)$`,'m'))?.[1].trim(); if(v===undefined) throw Error(key); return /^\d+$/.test(v)?Number(v):v; }
const p=key=>read(policyText,key), u=key=>read(powerText,key);
const wb=Workbook.create();
function sheet(name,title,note,headers,rows,widths,tableName,height=64){
  const s=wb.worksheets.add(name); s.showGridLines=false;
  const n=headers.length, last=String.fromCharCode(64+n), end=6+rows.length;
  const all=s.getRange(`A1:${last}${end}`);
  all.format.font={name:'Arial',size:11,color:'#202B3C'};
  all.format.verticalAlignment='center'; all.format.rowHeight=height;
  s.getRange(`A1:${last}5`).format.rowHeight=23;
  s.getRange('A2').values=[[title]]; s.getRange('A2').format.font={size:16,bold:true,color:'#243D67'};
  s.getRange('A3').values=[[note]];
  s.getRange('A4').values=[['修改此表不会自动改变游戏；新增入口需同步代码与配置。']];
  s.getRange('A3:A4').format.font={size:10,color:'#596779'};
  s.getRange(`A6:${last}${end}`).values=[headers,...rows];
  s.getRange(`A6:${last}${end}`).format.wrapText=true;
  const t=s.tables.add(`A6:${last}${end}`,true,tableName); t.showFilterButton=true;
  s.getRange(`A6:${last}6`).format={fill:'#243D67',font:{name:'Arial',size:11,bold:true,color:'#FFFFFF'},horizontalAlignment:'center',verticalAlignment:'center',rowHeight:34,wrapText:true};
  for(let i=0;i<n;i++) s.getRange(`${String.fromCharCode(65+i)}1:${String.fromCharCode(65+i)}${end}`).format.columnWidthPx=widths[i];
  s.freezePanes.freezeRows(6); s.freezePanes.freezeColumns(2);
  return s;
}
const headers=['业务编号（建议）','广告入口','类型','触发时机','展示前提','成功结果','频控规则','取消／失败处理','平台广告位 ID','状态'];
const rv=p('rewardedAdUnitId'), inter=p('interstitialAdUnitId');
const reward=`对应道具 +${u('rewardedAmount')}；保存库存，不自动使用`;
const rvGate='教学已完成、正常游玩；该道具库存为 0；弹窗内主动点击 Watch Ad；广告就绪且无其他广告';
const rvFreq=`三种道具共享激励冷却 ${p('rewardedRequestIntervalSeconds')} 秒；无单独次数上限；请求与结束均刷新冷却及插屏保护`;
const rvFail='不发奖；弹窗显示暂不可用，可关闭后继续；重复／过期回调不重复发奖';
const isGate='胜利或失败结算即将显示，教学已完成；满足「公共规则」全部插屏门槛；广告已就绪';
const isFreq='所有结算入口共享同一套插屏频控，详见「公共规则」';
const isFail='未就绪立即显示结算；关闭／失败／超时后显示结算一次，不补播';
const entries=[
 ['RV_HINT','提示补充','激励视频','提示为 0 → 点击提示 → 点击 Watch Ad',rvGate,reward,rvFreq,rvFail,rv,'已接入／开关开'],
 ['RV_ERASER','擦除补充','激励视频','擦除为 0 → 点击擦除 → 点击 Watch Ad',rvGate,reward,rvFreq,rvFail,rv,'已接入／开关开'],
 ['RV_EXTRA_MOVES','加步道具补充','激励视频','加步道具为 0 → 点击加步 → 点击 Watch Ad',rvGate,reward+'；之后每使用 1 个加 3 步',rvFreq,rvFail,rv,'已接入／开关开'],
 ['IS_RESULT_WIN','胜利面板显示前','插屏','胜利动画与步数转分结束 → 结算面板显示之前',isGate,'广告结束后显示胜利面板；下一关／Home 立即执行',isFreq,isFail,inter,'已接入／开关开'],
 ['IS_RESULT_FAIL','失败面板显示前','插屏','失败确定后的结算延迟结束 → 失败面板显示之前',isGate,'广告结束后显示失败面板；Retry／Home 立即执行',isFreq,isFail,inter,'已接入／开关开']
];
const main=sheet('广告入口','Arrow Tones！ 广告策略','核对日期：2026-11-24。已接入代表代码和配置就绪；平台启用与真实投放仍需验证。',headers,entries,[180,155,95,250,300,270,275,270,225,160],'AdPlacements',72);
main.tabColor='#243D67';
const rules=[
 ['总开关','adsEnabled',p('adsEnabled')===1?'开启':'关闭','全部广告','关闭时不请求真实广告；Editor 道具模拟由独立开关控制'],
 ['激励开关','rewardedEnabled',p('rewardedEnabled')===1?'开启':'关闭','激励视频','还需有效广告位 ID、客户端支持和广告就绪'],
 ['插屏开关','interstitialEnabled',p('interstitialEnabled')===1?'开启':'关闭','插屏','还需有效广告位 ID、客户端支持和广告就绪'],
 ['首次道具库存','initialHints / initialErasers / initialExtraMoves',`${u('initialHints')} / ${u('initialErasers')} / ${u('initialExtraMoves')}`,'新本地存档','提示／擦除／加步各 0 个；已有库存不会被重置'],
 ['激励奖励','rewardedAmount',u('rewardedAmount'),'三个激励入口共享','只补充所选道具；仅完整观看回调发奖；不自动使用'],
 ['闯关保护','protectedCampaignLevels',p('protectedCampaignLevels'),'插屏','第 1～3 关结算不展示；无尽模式不按闯关编号保护'],
 ['通关门槛','winsBetweenInterstitials',p('winsBetweenInterstitials'),'插屏','会话先累计至少 3 次通关；每次确认插屏关闭后清零，再累计'],
 ['首次等待（秒）','firstInterstitialAfterSeconds',p('firstInterstitialAfterSeconds'),'插屏','本次广告控制器会话累计前台时间至少 120 秒；与其他门槛同时满足'],
 ['模块保护（秒）','运行时代码固定下限',15,'插屏','首次开始预加载后至少 15 秒；与会话等待同时判断'],
 ['插屏间隔（秒）','interstitialIntervalSeconds',p('interstitialIntervalSeconds'),'插屏','从上次确认关闭计时；运行时最小 30 秒'],
 ['激励后保护（秒）','rewardedGraceSeconds',p('rewardedGraceSeconds'),'插屏','激励请求及结束均刷新；取消、无填充、冷却内请求也保护（另一广告播放中被拒绝除外）'],
 ['会话上限（次）','maxInterstitialsPerSession',p('maxInterstitialsPerSession'),'插屏','每次广告控制器初始化重新计数；仅确认关闭增加计数；非每日上限'],
 ['激励冷却（秒）','rewardedRequestIntervalSeconds',p('rewardedRequestIntervalSeconds'),'激励视频','各道具共享；请求及结束刷新计时；冷却内再次请求也刷新'],
 ['失败退避（秒）','preloadRetrySeconds',p('preloadRetrySeconds'),'加载与插屏尝试','加载／播放失败后退避；插屏展示尝试也受退避保护'],
 ['预加载超时（秒）','preloadTimeoutSeconds',p('preloadTimeoutSeconds'),'全部广告','无响应则释放实例并退避；切换关卡不等待加载'],
 ['播放超时（秒）','playbackTimeoutSeconds',p('playbackTimeoutSeconds'),'全部广告','按前台时间；超时恢复操作且激励不发奖；运行时最小 120 秒'],
 ['成功后再加载（秒）','运行时代码',1,'全部广告','完成／取消后 1 秒才可再次预加载；不代表可绕过展示冷却'],
 ['广告互斥','运行时代码','同一时刻 1 个','全部广告','播放时暂停音频；结算插屏锁定结算输入；激励弹窗阻止棋盘操作'],
 ['未加载处理','运行时代码','不等待、不排队','全部广告','插屏立即显示结算面板；激励不发奖并提示不可用'],
 ['前后台','运行时代码','仅累计前台时间','全部广告','后台时间不计入会话时长与超时；恢复前台不自动补播'],
 ['不触发场景','当前入口逻辑','无广告','启动／教学／导航按钮／暂停／主菜单','Next Level、New Board、Endless Mode、Home、Retry 及棋盘操作均不触发插屏'],
 ['未接入类型','当前实现','无','Banner／开屏／复活／翻倍','这些类型／入口当前没有接入；新增前在模板登记并实现'],
 ['次数统计口径','运行时代码','确认关闭','插屏','预加载与 Show 调用不算成功关闭；失败不增加会话次数也不清空通关数'],
 ['埋点现状','HueTelemetry / EventTracker','已接入代码','广告与关卡漏斗','关卡进入／结果／离开、广告阶段和道具奖励接入现有 TikTok 上报接口；Editor 仅日志，后台到达需真机验证']
];
const common=sheet('公共规则','公共频控与奖励规则','表中参数为当前代码与配置快照；所有插屏门槛必须同时满足。',['规则','配置字段／来源','当前值','适用范围','规则说明'],rules,[220,350,170,250,540],'AdRules',42);
common.getRange('C7:C30').format.horizontalAlignment='center';
const sourceStart=34;
const sources=[
 ['核对依据','用途'],
 ['Assets/HueRelay/Resources/HueRelay/Data/AdPolicy.asset','当前广告开关、平台 ID、频控参数'],
 ['Assets/HueRelay/Resources/HueRelay/Data/PowerUps.asset','初始道具数量和激励奖励数量'],
 ['Assets/HueRelay/Scripts/Gameplay/HueAdPacing.cs','实际频控判断与计数重置'],
 ['Assets/HueRelay/Scripts/Gameplay/HueAds.cs','预加载、播放、超时、冷却与诊断'],
 ['Assets/HueRelay/Scripts/Gameplay/HueGame.Advertising.cs','胜利／失败结算面板前插屏入口'],
 ['Assets/HueRelay/Scripts/Gameplay/HueGame.cs','结算按钮动作及通关计数'],
 ['Assets/HueRelay/Scripts/Gameplay/HueGame.PowerUpRefill.cs','补充弹窗、请求与奖励去重'],
 ['当前架构','3 个激励入口共用 1 个平台 ID；2 个结算时机共用 1 个插屏 ID。按入口单配平台 ID 需扩展配置及后端。']
];
common.getRange(`A${sourceStart}:B${sourceStart+sources.length-1}`).values=sources;
common.getRange(`A${sourceStart}:B${sourceStart+sources.length-1}`).format={wrapText:true,rowHeight:55,font:{name:'Arial',size:10,color:'#596779'}};
common.getRange(`A${sourceStart}:B${sourceStart}`).format.font={bold:true};
const templates=[
 ['RV_NEW_001','填写入口名称','激励视频','填写具体页面、玩家操作和触发节点','填写库存／关卡／状态／自愿确认条件','填写奖励种类、数量、完整观看判定和存档方式','填写共享／独立冷却、每局／会话上限','填写失败、取消、超时、重复回调处理','填写真实平台 ID','待评审'],
 ['IS_NEW_001','填写入口名称','插屏','填写自然间隙和具体按钮动作','填写通关、教学保护、加载等前提','填写关闭后应继续的原操作','明确是否继承当前全局插屏频控','未加载直接继续；失败／关闭只继续一次','填写真实平台 ID','待评审'],
 ...Array.from({length:5},()=>Array(10).fill(null))
];
const draft=sheet('新增广告位模板','新增广告位登记模板','复制一行填写；黄色区域可编辑。模板示例均未接入，登记后再同步实现与验收。',headers,templates,[180,155,95,250,300,270,275,270,225,160],'NewAdPlacements',56);
draft.getRange('A7:J13').format.fill='#FFF5D9';
draft.getRange('C7:C13').dataValidation={rule:{type:'list',values:['激励视频','插屏']}};
draft.getRange('J7:J13').dataValidation={rule:{type:'list',values:['待评审','待开发','待验收','已启用','已停用']}};
draft.getRange('A16').values=[['新增入口实施步骤']];
draft.getRange('A16').format.font={bold:true,size:13};
const steps=[
 ['1. 登记','确定唯一业务编号、类型、触发、奖励、频控、失败回退和平台 ID。'],
 ['2. 接入','接入统一广告控制器；当前包含道具补充激励和胜利／失败面板前插屏，新增奖励类型需增加明确奖励回调。'],
 ['3. 配置','如需每个入口独立平台 ID、冷却或次数限制，先扩展配置与后端；当前按广告类型共享。'],
 ['4. 统计','广告阶段使用 ad_event 的 placement/detail，关卡使用 attempt_id 串联；新增入口同步扩展上下文，发布前验证后台到达。'],
 ['5. 验收','完整观看、提前关闭、无填充、断网、超时、连点、重复／过期回调、后台恢复、奖励保存与频控边界。'],
 ['6. 启用','确认平台广告位启用并完成目标客户端真机验证，更新状态与核对日期。']
];
draft.getRange('A17:B22').values=steps;
draft.getRange('A17:J22').format={wrapText:false,rowHeight:32,font:{name:'Arial',size:11}};
wb.recalculate();
console.log((await wb.inspect({kind:'table',range:'广告入口!A6:J13',include:'values',tableMaxRows:8,tableMaxCols:10,maxChars:1500})).ndjson);
for(const [name,range,file] of [['广告入口','A1:J13','entries'],['公共规则','A1:E30','rules'],['公共规则','A34:C42','sources'],['新增广告位模板','A1:J13','template'],['新增广告位模板','A16:J22','steps']]){
  const png=await wb.render({sheetName:name,range,scale:1,format:'png'});
  await fs.writeFile(`${out}/${file}-arrow-tones.png`,new Uint8Array(await png.arrayBuffer()));
}
const xlsx=await SpreadsheetFile.exportXlsx(wb);
await xlsx.save(`${out}/ArrowTones-广告策略.xlsx`);
console.log('Saved ArrowTones-广告策略.xlsx');
