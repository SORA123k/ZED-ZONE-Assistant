# ZED ZONE 联机兼容性技术档案（Phase-1 静态分析）

> 目标：为「生存助手」模组确定 **如何在联机环境下检测并拒绝触碰玩家状态**，并说明这一限制的技术成因。
>
> **本文全部结论来自静态分析，未在游戏中运行验证。** 任何「测试」栏一律为 `未实机验证`。
>
> 游戏目录 `<游戏目录>` 全程只读：未写入、未运行、未修改。

---

## 0. 证据等级与取证边界

### 0.1 标记约定

| 标记 | 含义 |
|---|---|
| 【事实-程序集】 | 从 `BepInEx\interop\Assembly-CSharp.dll` 的类型/字段/方法**签名**直接读出，签名可信 |
| 【事实-日志】 | 来自 `Player.log` / `Player-prev.log` 的实际记录 |
| 【官方公告】 | 来自官方公开发布渠道的说明 |
| 【推断】 | 基于签名与结构证据的合理推断，**未读到方法体** |
| 【未验证】 | 无法从静态分析判定，需 Phase-2 实机测试 |

### 0.2 关键前提：interop 是 trampoline，不是实现

`BepInEx\interop\Assembly-CSharp.dll` 是 Il2CppInterop 生成的代理程序集：

- **类型、字段名、字段类型、方法签名、virtual/override 修饰、static 成员 —— 准确可信**；
- **方法体不可见**，全部是 `il2cpp_runtime_invoke` 跳板。

推论：本文可以断言「`MsgPlayerState` 有一个 `float hp` 字段」，但**不能**断言「主机用它覆盖客户端血量」。所有「谁写谁读」的结论一律降级为【推断】，并在第 9 节列出待验证项。

### 0.3 三个必须强调的取证陷阱

1. **`[CallerCount(N)]` 是引用范围长度（`RefRangeEnd − RefRangeStart`），不是调用次数**，也不能说明谁调用了谁。本文未用它做任何调用关系推断。
2. **不能从名字推断行为。** 例：`MsgServerState` 形似"服务器状态"，实际字段是 `cells, hp, failureAt` —— 这是**游戏内的电器/服务器机柜设施**，与联机后端无关。
3. **`Player.log` 中没有联机证据。** 见 0.5。

### 0.4 本次实际使用的取证命令

```powershell
$env:DOTNET_ROOT="<dotnet>"; $env:DOTNET_ROLL_FORWARD="LatestMajor"
& "ilspycmd.exe" `
  "<游戏目录>\BepInEx\interop\Assembly-CSharp.dll" -t <TypeName>
```

`NetSim` 等类型在 `ZedZone.Net` 命名空间下，必须用全名（`-t ZedZone.Net.NetSim`），否则报 `Could not find type definition`。

### 0.5 日志证据的实际情况（重要更正）

任务简报称游戏日志「包含真实联机栈」。**实测不成立**，必须如实记录：

| 文件 | 大小 | 行数 | `ZedZone.Net` | `NetSim` | `isSimulationAuthority` | `LiteNetLib` | `NetPeer` |
|---|---|---|---|---|---|---|---|
| `Player.log` | 32,071 B | 395 | 0 | 0 | 0 | 0 | 0 |
| `Player-prev.log` | 651 B | 15 | 0 | 0 | 0 | 0 | 0 |

【事实-日志】两份日志均为**单机启动**记录（`GameController:Init` → 读设置 → 加载 Mod），**不含任何联机会话**。

日志唯一可用的价值是**证实真实源码布局**【事实-日志】，例如：

```
D:/Unity/Project/ZedZone/Assets/Scripts/Controllers/GameController.cs
D:/Unity/Project/ZedZone/Assets/Scripts/Character/Monsters/MonsterManager.cs
D:/Unity/Project/ZedZone/Assets/Scripts/Character/AI/GOAP/GOAPAgent.cs
D:/Unity/Project/ZedZone/Assets/Scripts/Mods/Modloader.cs
```

注意：这些栈里**没有**任何 `Scripts/Net/` 路径，进一步印证本次日志无联机活动。

> 另注：游戏根目录的 `changelog.txt` 是 **BepInEx v6 的更新日志**（条目全部是 BepInEx/BepInEx 依赖项如 Cpp2IL、Doorstop、Harmony），**不是游戏更新日志**，不能作为游戏版本或联机功能的证据。

### 0.6 「8 人联机（1.0, 2026-11-06）」这条前提

【未验证】本次静态分析**无法证实也无法证伪**玩家数上限。可确认的仅是：存在 `MsgSessionConfig.maxPlayers` 字段【事实-程序集】，说明人数上限是**主机可配置的会话参数**，不是硬编码常量。具体数值需实机或官方公告确认。

---

## 1. 联机架构判定

### 1.1 结论（先给判定）

**不是纯 P2P，也不是 dedicated server，而是「主机权威 + 星型中继」的混合模型。**

具体分三层，权威归属**不同**：

| 层 | 权威方 | 判定依据 |
|---|---|---|
| 世界模拟（怪物、地形、容器、交易、生产） | **主机** | `NetMonsterSync` 的 `Host*` / `Client*Puppet` 方法完全对称 |
| 玩家**自身**要害（hp/buff） | **客户端自报** | `SampleOwnState` → `MsgPlayerState.hp` → 接收端 `ApplyPuppetHp` |
| 玩家**死亡/倒地/复活** | **主机/网络下发** | `NetApplyLocalDeath`、`NetApplyLocalRespawn`、`NetEnterDowned` |
| 身份与准入 | Steam ticket + PlayFab 白名单 | `MsgHello.steamAuthTicket`、`CoopBetaGate` |

**这与「所有玩家状态都由主机权威」的常见假设不同，是本档案最重要的更正。**

### 1.2 传输层

【事实-程序集】`BepInEx\interop\` 同时包含 `LiteNetLib.dll` 与 `PlayFab.dll`；`NetworkManager` 的成员引用了 LiteNetLib 具体类型：

- 字段 `m_net`（`LiteNetLib.NetManager`，见 `rawNet` 属性 `get_rawNet_Internal_get_NetManager_0`）
- 字段 `m_listener`、`m_hostPeer`（`NetPeer`）
- 字段 `m_lanNet` / `m_lanListener`（LAN 发现）
- 字段 `m_peerSnapshot`、`m_peerByPlayerId`、`m_abortedPendingPeer`

→ 传输为 **LiteNetLib（UDP）**【事实-程序集】。

### 1.3 星型中继（不是全互联 mesh）

【事实-程序集】`NetPlayerSync` 里同一份状态存在成对的方法，命名揭示了转发路径：

```
OnHostReceiveState(MsgPlayerState, NetPeer)      // 主机收到某客户端自报
HostBroadcastStates()                            // 主机广播
HostRelayFreshStates()                           // 主机中继"新鲜"状态
SendStatesExcluding(bool, NetPeer, int, int)     // 排除某 peer 再发
EncodeRelayBatch(NetDataWriter, MsgPlayerStateBatch)
OnClientReceiveStateBatch(MsgPlayerStateBatch, NetPeer)   // 客户端收批量
```

同一模式在怪物侧更直白：`NetMonsterSync.HostSendToClientsKnowing(...)`、`HostSendStates(uint)`、`ClientUpdatePuppets()`。

→ 玩家状态走 **客户端 → 主机 → 其他客户端** 的两跳转发【推断，基于方法命名与成对结构】。客户端之间**不直接互联**。

### 1.4 主机权威的证据（怪物侧最硬）

【事实-程序集】`ZedZone.Net.NetMonsterSync` 方法集严格分为两组：

**仅主机侧：**
```
HostReset()  HostSendStates(uint)  HostSendEquipment(HumanCharacterController)
HostResendSpawn(BasicCharacterController)  HostNotifyDeactivated(BasicCharacterController)
HostTryGetControllerByNetId(int, out BasicCharacterController)
HostTryGetNetId(BasicCharacterController, out int)
HostUpdateInterest()  HostTickCorpsewheel(...)  AuditCollectIds(int, List<int>)
SampleMonster(BasicCharacterController) -> MonsterTickSample
```

**仅客户端侧：**
```
SpawnPuppet(MsgMonsterSpawn)  ClientDrainSpawnQueue()  ClientUpdatePuppets()
RemovePuppet(int, bool)  ClientDespawnAll()  ClientGetPuppet(int)
ClientTryGetPuppetNetId(BasicCharacterController, out int)
ClientApplyCorpsewheelState(MonsterPuppet)
OnClientReceiveStateBatch(MsgMonsterStateBatch, NetPeer)
OnClientReceiveSpawn(MsgMonsterSpawn, NetPeer)
```

并且存在内嵌类型 **`NetMonsterSync.MonsterPuppet`**【事实-程序集】。

→ **怪物由主机模拟，客户端只跑 `MonsterPuppet` 影子。** 先前调查的这一条**得到独立验证**。

### 1.5 PlayFab 的作用：不参与游戏状态权威

【事实-程序集】`ZedZone.Net` 下有 `CoopBetaGate`、`CoopBetaAuth`、`CoopBetaWatermark`、`NetSteamTicket`、`NetCoordClient`、`NetCoordProtocol`。

`CoopBetaGate` 的成员全部围绕**准入白名单**：

```
public static bool isReady { get; }
public static bool IsLocalWhitelisted { get; }
public static int LocalTesterCode { get; }
public static bool IsWhitelisted(ulong steamId)
public static bool TryGetCode(ulong steamId, out int code)
public static void EnsureStarted()  public static void RequestRefresh()
static field: TitleDataKey
```

`TitleDataKey` 表明它读的是 PlayFab **TitleData**（标题级配置），`IsWhitelisted(ulong steamId)` 是**按 SteamID 查白名单**。

【推断】PlayFab 承担：合作模式 beta 白名单、Steam 票据校验、房间/协调服务（`NetCoordClient`）。**没有任何 `Msg*` 类型用于走 PlayFab 同步游戏状态** —— 221 个 `Msg*` 全部是会话内协议消息，由 `NetworkManager` 经 LiteNetLib 投递。

→ **游戏玩法状态不存在服务端（PlayFab）权威，只有主机权威。** 这一点直接影响第 5 节：没有第三方服务端做数值校验。

### 1.6 准入链（解释为何需要 Steam ticket）

【事实-程序集】`MsgHello` 字段：`gameVersion, playerName, modHash, steamAuthTicket, steamId, characterId, hardcoreCharacter, roomPasswordHash`

`MsgWelcome`：`yourPlayerId, hostGameVersion, members`

`NetKickReason` 枚举【事实-程序集】：
```
None, ServerFull, VersionMismatch, ModMismatch, HostShutdown, HelloTimeout,
Kicked, ModSyncTimeout, ModFileMissing, HostWorldLoading, NotWhitelisted,
AuthFailed, AuthTimeout, HardcoreForbidden, Banned, BadPassword
```

→ **注意：`NetKickReason` 中没有 `Cheat` / `Desync` / `AuditMismatch` 等"数值异常"条目。** 这是第 5 节论证的关键事实。

### 1.7 连接状态机

【事实-程序集】

```csharp
public enum ZedZone.Net.NetMode        { Offline, Host, Client }
public enum ZedZone.Net.NetClientState { Disconnected, Connecting, Handshaking, Joined }
```

`NetworkManager` 相关公开成员：

```csharp
public static NetworkManager instance          // 静态单例
public static bool isOnline   { get; }
public static bool isHost     { get; }
public static bool isClient   { get; }
public static bool worldMessagesAllowed { get; }
public static bool resettingWorld       { get; }
public static uint worldGeneration      { get; }
public NetMode mode                 { get; }   // 实例属性
public NetClientState clientState   { get; }   // 实例属性
public int localPlayerId            { get; }
public string localPlayerName       { get; }
public static bool BetaCoopBlocked(string what)
public static NetworkManager EnsureInstance()
```

---

## 2. 玩家状态是如何同步的

### 2.1 全部 `Msg*` 类型枚举（共 221 个，已完整覆盖）

【事实-程序集】`ZedZone.Net` 下 `Msg` 前缀类型合计 **221 个**，下表的 12 组**互斥且并集恰为 221**（已用脚本校验：19+7+22+22+41+38+26+25+10+5+4+2 = 221）。

| 组 | 数量 | 类型清单 |
|---|---|---|
| A 玩家本体 | 19 | `MsgPlayerState`, `MsgPlayerStateBatch`, `MsgPlayerBuff`, `MsgPlayerSpawn`, `MsgPlayerDespawn`, `MsgPlayerJoinWorld`, `MsgPlayerDeath`, `MsgPlayerRespawn`, `MsgPlayerDowned`, `MsgPlayerGiveUp`, `MsgPlayerEquipment`, `MsgPlayerHoldItem`, `MsgPlayerCarry`, `MsgPlayerRoll`, `MsgPlayerClimb`, `MsgPlayerJumpOver`, `MsgPlayerAction`, `MsgPlayerVomit`, `MsgPlayerHitRequest` |
| B 怪物 | 7 | `MsgMonsterSpawn`, `MsgMonsterDespawn`, `MsgMonsterStateBatch`, `MsgMonsterAnim`, `MsgAttackAnim`, `MsgLizardState`, `MsgFinalBossState` |
| C 部落/Horde/灾变 | 22 | `MsgHordeSpawnBatch`, `MsgHordeDespawnBatch`, `MsgHordeStateBatch`, `MsgHordeWaveState`, `MsgHordeCataclysm`, `MsgHordeSupplyDrop`, `MsgHordeProgress`, `MsgHordeCampRepair`, `MsgHordeCampGun`, `MsgHordeDecayItems`, `MsgHordeResult`, `MsgHordePlayerDamage`, `MsgHordeDamageRequest`, `MsgHordePlayerCard`, `MsgHordeNotice`, `MsgCataclysmState`, `MsgCataclysmRunResult`, `MsgAlarmRequest`, `MsgAlarmSiren`, `MsgFlareAlarm`, `MsgNoiseRequest`, `MsgServerState` |
| D 战斗与特效 | 22 | `MsgHitRequest`, `MsgDamageEvent`, `MsgImpactKnockdown`, `MsgProjectileFire`, `MsgSpecialBeam`, `MsgSpecialFxBurst`, `MsgHitFx`, `MsgThrow`, `MsgDeployRequest`, `MsgDeployableHit`, `MsgArenaState`, `MsgArenaArmHit`, `MsgFactoryAction`, `MsgFactoryActionResult`, `MsgFactoryMechState`, `MsgFactoryOrdnance`, `MsgMechAim`, `MsgTurretStateBatch`, `MsgTurretStateEntry`, `MsgTurretOp`, `MsgTurretOpResult`, `MsgNpcEquipment` |
| E 物品/容器/交易/生产 | 41 | `MsgGroundItemSpawn`, `MsgGroundItemRemove`, `MsgGroundItemUpdate`, `MsgPickupRequest`, `MsgPickupGrant`, `MsgPickupDeny`, `MsgDropRequest`, `MsgContainerOpenRequest`, `MsgContainerOpenReply`, `MsgContainerClose`, `MsgContainerActionQuery`, `MsgContainerActionReply`, `MsgSharedInvOp`, `MsgSharedInvOps`, `MsgSharedInvSnapshot`, `MsgQuickStackRequest`, `MsgQuickStackReply`, `MsgCorpseConsumed`, `MsgCorpseLiftRequest`, `MsgCorpseLiftGrant`, `MsgCorpsewheelState`, `MsgGrinderNotice`, `MsgAmmoRecipeRead`, `MsgMerchantOpenRequest`, `MsgMerchantStock`, `MsgMerchantTradeRequest`, `MsgMerchantTradeResult`, `MsgStovePackRequest`, `MsgStovePackResult`, `MsgStoveState`, `MsgProductionState`, `MsgProductionOpenRequest`, `MsgProductionOpenReply`, `MsgProductionClose`, `MsgProductionResyncRequest`, `MsgProductionSwitch`, `MsgStationOpenRequest`, `MsgStationOpenReply`, `MsgStationClose`, `MsgStationUpdate`, `MsgStationSeqState` |
| F 地形/建造/种植/涂装 | 38 | `MsgTerrainPlace`, `MsgTerrainPlaced`, `MsgTerrainRemove`, `MsgTerrainDismantle`, `MsgTerrainBuildFinish`, `MsgTerrainDig`, `MsgTerrainHitRequest`, `MsgTerrainHp`, `MsgTerrainState`, `MsgTerrainRepairRequest`, `MsgTerrainRepairResult`, `MsgVoxelEdit`, `MsgVoxelEditRequest`, `MsgVoxelEditResult`, `MsgVoxelHit`, `MsgBuildingWallHit`, `MsgBuildingWallState`, `MsgBuildingRepairRequest`, `MsgBuildingRepaired`, `MsgBuildingRepairResult`, `MsgTileStateBatch`, `MsgBuildFeed`, `MsgBuildFeedRejected`, `MsgGrassCutRequest`, `MsgGrassCutResult`, `MsgGrassStateBatch`, `MsgGrassStateQuery`, `MsgPaintTextureRequest`, `MsgPaintTexturePayload`, `MsgTextureSignPaint`, `MsgGateCardInsert`, `MsgGateCardInsertResult`, `MsgCropAction`, `MsgCropState`, `MsgCropActionResult`, `MsgGroundDecal`, `MsgFireworkIgnite`, `MsgFishingState` |
| G 载具/交通 | 26 | `MsgVehicleSpawn`, `MsgVehicleDespawn`, `MsgVehicleSpawnAck`, `MsgVehicleState`, `MsgVehicleSeatRequest`, `MsgVehicleSeatReply`, `MsgVehicleSeat`, `MsgVehicleDataUpdate`, `MsgVehicleInspectRequest`, `MsgVehicleInspectReply`, `MsgVehiclePartOp`, `MsgVehicleFuelTransferRequest`, `MsgVehicleFuelTransferResult`, `MsgVehicleTrunkPartInstallRequest`, `MsgVehicleTrunkPartInstallResult`, `MsgBicycleRequest`, `MsgBicycleReply`, `MsgBicycleState`, `MsgBicycleRemove`, `MsgTrainState`, `MsgTrainRequest`, `MsgTrainReply`, `MsgLiftRequest`, `MsgLiftReply`, `MsgLifterTakeRequest`, `MsgLifterTakeResult` |
| H 世界/地图/时间/睡眠/岛屿 | 25 | `MsgWorldInfo`, `MsgWorldClock`, `MsgChunkRequest`, `MsgChunkData`, `MsgDormantZones`, `MsgForkLoad`, `MsgMapExplored`, `MsgMapExploredRequest`, `MsgWaypoint`, `MsgLocationMarkerRequest`, `MsgLocationMarkerSync`, `MsgNpcMapRequest`, `MsgNpcMapSync`, `MsgSleepState`, `MsgSleepVote`, `MsgSleepVoteRequest`, `MsgSleepVoteReply`, `MsgRespawnAnchorRequest`, `MsgRespawnAnchorState`, `MsgIsland2State`, `MsgIsland2Action`, `MsgIsland2ActionResult`, `MsgIsland2Ending`, `MsgIslandTravel`, `MsgBuffFx` |
| I 会话/大厅/握手 | 10 | `MsgHello`, `MsgWelcome`, `MsgMemberUpdate`, `MsgKick`, `MsgChat`, `MsgEcho`, `MsgSessionConfig`, `MsgLobbyState`, `MsgLobbyProfile`, `MsgLobbyReturnReady` |
| J Mod 同步 | 5 | `MsgModManifest`, `MsgModFileRequest`, `MsgModFileChunk`, `MsgModFileAck`, `MsgModSyncReady` |
| K 救援 | 4 | `MsgAidRequest`, `MsgAidResult`, `MsgAidState`, `MsgAidApply` |
| L 审核 | 2 | `MsgAuditReport`, `MsgAuditReply` |

> 每个类型**恰好出现一次**，无重复、无遗漏（已用脚本枚举比对）。两处易混的归类：`MsgLifterTakeRequest/Result` 归入 G（升降机/搬运工属交通设施），`MsgCorpsewheelState` 归入 E（尸体轮盘产出物品），`MsgNpcEquipment` 归入 D（NPC 装备由战斗侧同步）。

### 2.2 要害数值（hp/hunger/thirst/stamina/fatigue）的同步情况 —— 核心结论

**这是本档案最关键的一节。**

【事实-程序集】对 221 个 `Msg*` 全量扫描「字段是否含 `hunger/thirst/stamina/fatigue/sanity/hp/health/buff/energy`」，命中结果**仅**：

| 消息 | 命中字段 |
|---|---|
| `MsgPlayerState` | `hp`, `qHp` |
| `MsgPlayerBuff` | `buffId`, `buffEndTime` |
| `MsgBuffFx` | `buffId`, `buffEndTime` |
| `MsgPlayerDowned` | `hpPercentOnRevive` |
| `MsgCataclysmState` | `hpFraction` |
| `MsgCorpsewheelState` | `hp`, `maxHp` |
| `MsgArenaState` | `armHp`, `armMaxHp` |
| `MsgServerState` | `hp`（电器设施，非玩家） |
| `MsgTerrainHp` / `MsgVehiclePartOp` / `MsgVehicleTrunkPartInstallResult` | `hp`（物件，非玩家） |
| `MsgHordeResult` / `MsgHordeWaveState` | `campHp`/`hpKilled` 等（营地/统计，非玩家） |
| `MsgPlayerState` 之外 | **无任何消息含 `hunger` / `thirst` / `stamina` / `fatigue` / `sanity`** |

**结论一：不存在周期性同步饥饿/口渴/耐力/疲劳/理智的消息。** 【事实-程序集】

**结论二：这些数值只在"加入世界/生成"时随 `CharacterData` 二进制块一次性传输。**【事实-程序集】

- `MsgPlayerJoinWorld.characterDataBytes`
- `MsgPlayerSpawn.characterDataBytes`（字段：`playerId, playerName, characterDataBytes, posX, posY`）
- `NetPlayerSync.SerializeCharacterData(CharacterData) / DeserializeCharacterData(...)`
- `CharacterData` 持有字段 **`characterStatusData`**（另有 `characterEquipmentData`, `characterPropertyData`, `bodypartsData`, `buffDataList`, `deadFlag`, `netPendingRespawn`, `saveFlag`）

**`CharacterStatusData` 完整字段**【事实-程序集】：

```
maxhp  hp
maxEnergy  energy  casEnergy
maxFatigue  fatigue
maxHunger  hunger
maxThirst  thirst
maxSanity  sanity
maxBloodVolume  bloodVolume  deadBloodVolume
standardBodyTemperature  bodyTemperatureOffsetFloat  minBodyTemperature  maxBodyTemperature  m_bodyTemperature
radiationDose
```

### 2.3 持续同步的那一份：`MsgPlayerState` 精确字段

【事实-程序集】`ZedZone.Net.MsgPlayerState` 全部字段（33 个）：

```
int playerId        float posX   float posY
float moveX         float moveY
float lookX         float lookY
float hp            float speed
int stateFlags      int liftAttrId
uint tick           int zLevel     uint teleportCounter
int standVz
int exoskeletonFlags  float exoskeletonDirectionX  float exoskeletonDirectionY
float viewCenterX  float viewCenterY  float viewHalfX  float viewHalfY
// 量化副本（NetQuant）
int qPosX  int qPosY  uint qMove  uint qLook  uint qHp  uint qSpeed
int qViewDX  int qViewDY  uint qViewHX  uint qViewHY  uint qExoDir
```

要点：

- 这是**位姿 + 状态标志 + hp** 的混合打包，`*Q*` 字段是量化冗余（`NetQuant`）。
- **只有 `hp`，没有 hunger/thirst/stamina/fatigue/sanity。**
- `teleportCounter`、`zLevel` 用于抑制"瞬移被当成移动"的误判。

### 2.4 同步方向：玩家自身要害是"客户端自报"

【事实-程序集】`NetPlayerSync` 的方法链：

```
SampleOwnState(uint tick)                        // 采样自己的状态
ShouldSendOwnState(MsgPlayerState) -> bool       // 节流/去重
WireDiff(int, MsgPlayerState, MsgPlayerState, bool)   // 差分编码
SendStateWriter(...) / EncodeRelayBatch(...)
--
OnHostReceiveState(MsgPlayerState, NetPeer)      // 主机收到（对端自报）
HostBroadcastStates() / HostRelayFreshStates()
--
OnClientReceiveStateBatch(MsgPlayerStateBatch, NetPeer)
PushSnap(...)   UpdatePuppets()
ApplyPuppetHp(int playerId, float hp)            // 只改 RemotePlayer 影子
```

`NetPlayerSync.RemotePlayer` 内嵌类型与 `m_remotes` 字典表明：**远端玩家在本地是"影子"对象**，hp 通过 `ApplyPuppetHp` 写到影子上。

【推断】因为 `SampleOwnState` 采样的是**本地玩家**、`ApplyPuppetHp` 只作用于**远端影子**，所以**本地玩家的 hp 在本地是自报的，没有被主机回写的路径**。主机收到后只做转发（`HostBroadcastStates` / `HostRelayFreshStates`），不重算。

**这直接否定了"改了就会被主机覆盖"的直觉。真正的后果见第 5 节。**

### 2.5 网络会主动改写本地玩家的路径（第 5 节的核心证据）

【事实-程序集】`HumanCharacterController` 上存在一组 `NetApply*` / `Net*` 方法，是**网络 → 本地玩家**的入口：

```
NetApplyLocalDeath(Vector2, out int, out string, out InventoryData)
NetApplyLocalRespawn(Vector2)
NetEnterDowned()        NetExitDowned(float)      NetSetDownedVisual(bool)
NetSetAidVisual(bool)   NetApplyVomit(bool)
NetSetBodyHidden(bool)
NetApplyHoldingItemVisual(int)
NetApplyNPCEquipment(CharacterEquipmentData, bool, bool, int)
NetApplyVisualBuff(int buffId, float ...)        // 在 BasicCharacterController 上
NetPlayFarmAction(int, Vector3, int, bool)
NetReplayJumpOver(Vector2, Vector2, float)
NetPlayConsumeHandAnimation()
```

【推断】这些是**死亡、复活、倒地、呕吐、外观、buff 视觉**的强制下发点。注意：

- **有** hp 相关：无直接的 `NetSetHp`（hp 不走这条），但死亡/倒地会改变存活语义。
- **完全没有** `NetApplyHunger` / `NetApplyThirst` / `NetApplyStamina` / `NetApplyFatigue` —— 无对应下发入口。

### 2.6 玩家状态的其它分类

| 类别 | 消息 | 方向【推断】 |
|---|---|---|
| 位姿/移动 | `MsgPlayerState`, `MsgPlayerStateBatch` | 客户端 → 主机 → 广播 |
| 要害 hp | `MsgPlayerState.hp` / `qHp` | 客户端自报 → 转发 |
| Buff | `MsgPlayerBuff(buffId, buffEndTime)`, `MsgBuffFx` | 转发，接收端做视觉 |
| 死亡 | `MsgPlayerDeath(playerId, posX, posY, zLevel, corpseConfigStr, corpseItemsBytes, standVz)` | 含**尸体背包**快照 |
| 复活 | `MsgPlayerRespawn(playerId, posX, posY, zLevel, spawnSource, movedAshore)` | 主机/网络定 |
| 倒地 | `MsgPlayerDowned(playerId, downed, remainMs, hpPercentOnRevive, died, posX, posY, zLevel, rescuerPlayerIdPlusOne)` | 主机定 |
| 放弃 | `MsgPlayerGiveUp` | — |
| 装备 | `MsgPlayerEquipment` | 客户端报，含签名去重 `BuildEquipmentSignature` |
| 手持 | `MsgPlayerHoldItem` | 客户端报 |
| 搬运 | `MsgPlayerCarry` | 客户端报 |
| 翻滚/攀爬/跳跃 | `MsgPlayerRoll`, `MsgPlayerClimb`, `MsgPlayerJumpOver` | 客户端报，对端播放动画 |
| 动作 | `MsgPlayerAction` | 客户端报 |
| 呕吐 | `MsgPlayerVomit` | 客户端报 |
| **玩家受击请求** | `MsgPlayerHitRequest(directional, targetPlayerId, damage, hitX, hitY, forceX, forceY, friendlyReduced)` | **请求式**，含 `friendlyReduced`（友伤削减） |

`MsgPlayerHitRequest` 是唯一"**带 damage 字段并且目标是玩家**"的消息，且字段名是 *Request*（请求，非 *Event*）。对比怪物侧有 `MsgHitRequest`（`targetNetId, damage, armorPenetration, bodyPartIndexPlusOne, ...`）与 `MsgDamageEvent`。

【推断】玩家对玩家的伤害是**请求 → 权威端裁决**的模型（`friendlyReduced` 需要权威方根据友伤开关计算），与"自身 hp 自报"并不矛盾：**受击是别人对你造成的，故需裁决；你自己的 hp 数值本身是自报的。**

---

## 3. `isSimulationAuthority` 的确切语义

### 3.1 精确签名【事实-程序集】

```csharp
namespace ZedZone.Net;

public static class NetSim : Il2CppSystem.Object
{
    public static GameData guestWorldGameData { get; set; }   // 静态字段（有 setter）
    public static bool isSimulationAuthority        { get; }  // ← 仅 getter
    public static bool allowLocalCharacterSpawn     { get; }  // ← 仅 getter
    public static bool allowGameplayPause           { get; }  // ← 仅 getter
    public static bool advanceWorldEvents           { get; }  // ← 仅 getter
    public static bool realtimeWorkProgress         { get; }  // ← 仅 getter
    public static bool allowLocalWorldSave          { get; }  // ← 仅 getter
    public static bool allowLocalGroundItemSpawn    { get; }  // ← 仅 getter
    public static bool allowLocalLootSpawn          { get; }  // ← 仅 getter
    public static bool allowLocalVehicleSpawn       { get; }  // ← 仅 getter
    public static bool singlePetSession             { get; }  // ← 仅 getter
}
```

### 3.2 作用域【事实-程序集】

- **全局静态**：`public static class`，所有成员都是 `static`。**没有实例成员、没有 per-player 维度。**
- 因此它描述的是**"本进程在世界模拟中的角色"**，不是"某个玩家"。

### 3.3 不可写（这一点必须强调）【事实-程序集】

interop 元数据里 `isSimulationAuthority` 与全部 `allowLocal*` **只有 getter，没有 setter**：

```
NativeMethodInfoPtr_get_isSimulationAuthority_Public_Static_get_Boolean_0
NativeMethodInfoPtr_get_allowLocalCharacterSpawn_Public_Static_get_Boolean_0
...（无任何 set_ 对应项）
```

唯一的可写静态成员是 `guestWorldGameData`（有 get/set）。

**推论：这些是"计算属性"，由内部状态推导，不是外部可赋值的开关。**【推断】

- 你无法通过 `NetSim.allowLocalXxx = true` 来改它（编译期即不存在该属性 setter）。
- 若强行用 `il2cpp_field_static_set_value` 去写其**私有后备字段**或 patch getter，等于**伪造整个权限模型**。见 3.5。

### 3.4 语义（【推断】，需 Phase-2 验证）

结合命名、同族成员与结构证据：

| 成员 | 推断语义 |
|---|---|
| `isSimulationAuthority` | "**本机是否负责模拟世界**" → 按 `NetMode` 推导，最可能是 `mode == Offline \|\| mode == Host` |
| `allowLocalCharacterSpawn` | 是否允许本地直接生成角色（客户端应为 false，角色由主机 `MsgPlayerSpawn` 下发） |
| `allowLocalGroundItemSpawn` / `allowLocalLootSpawn` / `allowLocalVehicleSpawn` | 客户端不得本地刷地面物品/战利品/载具（须由主机生成后同步） |
| `allowLocalWorldSave` | 客户端不得本地存档（世界归主机） |
| `advanceWorldEvents` / `realtimeWorkProgress` | 世界事件推进 / 生产进度是否本地实时推进（客户端应由主机时钟驱动） |
| `allowGameplayPause` | 联机时不接受本地暂停 |
| `singlePetSession` | 宠物/单例会话标志 |

**关键警示（对门禁设计的直接影响）：**

> `isSimulationAuthority` 的语义是「**我是不是模拟方**」，**不是**「**我是不是联机**」。
> 在联机中**主机**上它应为 `true`。因此
> **`!isSimulationAuthority` 不能用作"是否联机"的判据** —— 用它做门禁会在主机上放行。

`allowGameplayPause` 的 `[CallerCount(0)]`（`RefRangeStart=0, RefRangeEnd=0`）暗示该属性在本程序集版本中**可能未被引用**；但这只是引用范围长度，**不能据此断言它是死属性**。【未验证】

### 3.5 为什么绝不能写 `allowLocal*`

四条独立理由：

1. **没有 setter**（3.3）——写不了，除非 patch getter 或写私有字段，属"伪造权限模型"。
2. **它们是整套一致性的开关**：`allowLocalWorldSave` / `allowLocalGroundItemSpawn` / `allowLocalCharacterSpawn` 互相配合维持"客户端不产生权威数据"的不变式。单独把某个从 `false` 改成 `true`，会让**本地产生主机不知道的数据**（本地刷怪/刷物品/存档），这些数据不会进入主机的世界状态，最终表现为**本地与主机永久性分叉**。
3. **与审核机制冲突**：`NetStateAudit` 会对 `itemIds` / `monsterIds` / `vehicleIds` 做集合比对（见 5.2）。本地私刷的物品/怪物会直接体现为 `itemExtra` / `monsterExtra`。
4. **无收益**：本模组第一版的目标是单机，联机一律禁用（第 6 节）。写权限位既不必要，且风险最高。

> 先前调查的这条警告**成立且应保留**，其真正理由不是"会被检测"，而是"会破坏客户端-主机一致性不变式，且根本无法合法写入"。

### 3.6 谁检查 `isSimulationAuthority`

【未验证】—— 这是本文**无法给出确定性答案**的一项，原因见 9.1：

- interop 只含声明，**不含调用点**；
- `MethodXrefScanCache.db`（46 MB，magic `UMXC`）确实存在，`[CachedScanResults(RefRangeStart=821745, RefRangeEnd=822027)]` 就是它带出的索引区间，但该库**只存数字 id，不含任何方法名字符串**（已实测：在其中搜索 `isSimulationAuthority`、`SampleOwnState`、`allowLocalCharacterSpawn` 均 0 命中），无法反解到方法名；
- 游戏目录中**没有** IL2CPP dump（无 `il2cpp_dump.json` / `dump.cs` / `DummyDll`），无法做 xref 反查。

**能确定的结构性证据**：`isSimulationAuthority` 的 `[CallerCount(282)]` 说明其引用区间长度约 282（**不是调用次数**），即它是**被大量代码引用**的核心开关；而 `NetMonsterSync` / `NetPlayerSync` / `NetStateAudit` 等所有 `Net*` 同步器都持有 `get_net_Private_Static_get_NetworkManager_0` 并实现 `OnNetStateChanged`，说明"网络状态变化"通过 `NetworkManager.onStateChanged` 事件广播给各同步器。

【推断】各 `Net*Sync` 在 `OnNetStateChanged` 中重算自己的角色，并据 `NetSim.isSimulationAuthority`（或内部的同义量）决定走 `Host*` 还是 `Client*` 分支。这与 1.4 的 Host/Client 方法对称完全吻合。

---

## 4. 如何检测"当前是否处于联机"

### 4.1 分级信号表（按可靠性排序）

| # | 信号（精确签名） | 可靠性 | 玩家生成前可用？ | 说明 |
|---|---|---|---|---|
| 1 | `NetworkManager.isOnline` — `public static bool isOnline { get; }` | 【推断】最高 | **是** | 语义最贴近"是否联机"。`NetworkManager` 是常驻单例，早于玩家生成就存在 |
| 2 | `NetworkManager.instance.mode` — `public NetMode mode { get; }`，`NetMode { Offline, Host, Client }` | 【推断】最高 | **是** | 三态最清晰，可区分单机/主机/客户端；**推荐作为主判据** |
| 3 | `NetworkManager.isHost` / `isClient` — `public static bool` | 【推断】高 | 是 | 与 #2 冗余但独立，适合做交叉校验 |
| 4 | `NetworkManager.instance.clientState` — `NetClientState { Disconnected, Connecting, Handshaking, Joined }` | 【推断】中高 | 是 | **仅客户端侧**有意义；主机上无意义 |
| 5 | `NetworkManager.worldMessagesAllowed` / `resettingWorld` | 【推断】中 | 是 | 世界消息闸门/世界正在重置；适合做"此刻不要动手"的补充闸 |
| 6 | `NetPlayerSync.remotePlayerCount` — `public static int remotePlayerCount { get; }` | 【推断】中 | 否（需世界就绪） | >0 说明确实有别人；但**主机独占房间时为 0**，不能判"是否联机" |
| 7 | `NetPlayerSync.worldReady` / `worldTerrainReady` — `public static bool` | 【推断】中 | 否 | 世界/地形同步完成标志，适合做"可以安全读了"的前置 |
| 8 | `NetSim.isSimulationAuthority` | **不可用于判联机** | 是 | 见 3.4：主机为 true、单机也为 true |
| 9 | UI 面板 `MultiplayerPanel` / `SinglePlayerModePanel` | 【推断】弱 | 是 | 面板可能被实例化但未激活；状态易滞后。仅作日志/提示，**不作门禁** |
| 10 | `CoopBetaGate.isReady` / `IsLocalWhitelisted` | 【推断】无关 | 是 | 这是**beta 白名单准入**，与"当前是否在联机"无关 |

> 已确认存在 `MultiplayerPanel`（含 `RoomsFetchResult`、`PublicRoomEntry`、`LobbySeatRow` 等内嵌类型）与 `SinglePlayerModePanel`【事实-程序集】。但**面板存在 ≠ 处于联机**，仅作弱信号。

### 4.2 推荐的判定实现（单一入口，实时求值）

```csharp
internal static class NetGate
{
    // 唯一的联机判据；每次都实时求值，绝不缓存到启动时
    internal static bool IsMultiplayer()
    {
        // 主判据
        if (NetworkManager.isOnline) return true;

        // 兜底：isOnline 的语义若在某版本为 false 而 mode 仍非 Offline
        var nm = NetworkManager.instance;
        if (nm != null && nm.mode != NetMode.Offline) return true;

        return false;
    }
}
```

**为什么必须实时求值**：玩家可以从"单机存档"直接进入"联机房间"而**不重启进程**（`NetworkManager` 是常驻 MonoBehaviour，`onStateChanged` 事件存在【事实-程序集】）。在 `Awake` 里缓存一次判定结果会漏判。

**不要**使用 `!NetSim.isSimulationAuthority` 作为判据（3.4）。

---

## 5. 为什么在联机下改状态是不安全的

必须严格区分两种后果：**「会被覆盖」** 与 **「会被判定异常」**。二者在本游戏中**都不完全成立**，真实风险是第三种。

### 5.1 「会被覆盖」：只对部分字段成立

| 字段 | 会被网络覆盖？ | 依据 |
|---|---|---|
| `hp` | **否**（本地自报，无回写路径） | 2.4【推断】 |
| `hunger` / `thirst` / `fatigue` / `energy` / `sanity` | **否**（根本无周期同步消息） | 2.2【事实-程序集】 |
| 存活/死亡/倒地 | **是** | `NetApplyLocalDeath`、`NetEnterDowned`【事实-程序集】 |
| 复活位置 | **是** | `NetApplyLocalRespawn(Vector2)`【事实-程序集】 |
| buff 视觉 | **是** | `NetApplyVisualBuff(int, float)`【事实-程序集】 |
| 外观/手持视觉 | **是** | `NetApplyHoldingItemVisual`、`NetSetBodyHidden`【事实-程序集】 |
| 生成时的全量状态 | **是（重连/重生时）** | `MsgPlayerSpawn/JoinWorld.characterDataBytes`【事实-程序集】 |

→ **"改数值会被主机打回"不成立。** 这反而是更糟的情况：改动**不会**被纠正，而是被传播/持久化。

### 5.2 「会被判定异常」：`NetStateAudit` 不覆盖玩家数值

【事实-程序集】`ZedZone.Net.NetStateAudit` 是静态单例（`instance`），带 FNV-1a 常量（`FnvOffset`, `FnvPrime`）与 `autoIntervalSeconds`、`maxChunksPerAudit`、`m_nextAutoTime` 等字段，方法：

```
public static void TriggerNow()
SendReport() -> bool                       // 客户端上报
OnHostReceiveReport(MsgAuditReport, NetPeer)   // 主机收
OnClientReceiveReply(MsgAuditReply, NetPeer)   // 客户端收回复
ComputeObjectsHash(ChunkData, out int)  ComputeTileHash(ChunkData, float)  ComputeSoilHash(ChunkData)
IsClean(MsgAuditReply) -> bool   Summarize(MsgAuditReply) -> string
```

审核载荷【事实-程序集】：

```
MsgAuditReport(客户端→主机): seq, inGameTime, chunks, monsterIds, itemIds, vehicleIds, modSetHash
MsgAuditReply(主机→客户端): seq, clockDelta, chunksChecked, chunksSkipped,
                            mismatches, monsterMissing, monsterExtra,
                            itemMissing, itemExtra, vehicleMissing, vehicleExtra
```

**审核域 = 区块/地块/土壤哈希 + 怪物 id 集合 + 物品 id 集合 + 载具 id 集合 + mod 集合哈希。**

**玩家的 hp/hunger/thirst/stamina/fatigue/sanity 完全不在审核域内。** 【事实-程序集】

→ **直接改玩家要害数值，不会被 `NetStateAudit` 判为不一致。** "改数值触发哈希校验"这一担心**不成立**。

### 5.3 真正的不安全之处（这才是"不是偏执"的技术理由）

| 编号 | 失败模式 | 机制 | 等级 |
|---|---|---|---|
| **F1** | **改动被广播给所有人** | 本地 hp 经 `MsgPlayerState` 自报 → 主机 `HostRelayFreshStates` 转发 → 其他客户端 `ApplyPuppetHp` 显示你的假血量。作弊**可见** | 【推断】高 |
| **F2** | **存档污染 / 重连复现** | `MsgPlayerSpawn.characterDataBytes` 携带完整 `CharacterData`（含 `characterStatusData`）；主机侧持久化后，重进世界仍是你改过的值 | 【推断】高 |
| **F3** | **派生系统缓存分叉** | `BasicCharacterController` 还缓存 `maxHP`、`staminaRegenScale`、`characterData`；`UpdateStatus()` 每帧按旧缓存推进。只改一个字段会造成"UI 显示 A、逻辑用 B"的自相矛盾，并可能触发越界/除零 | 【推断】中高 |
| **F4** | **主机上改 = 影响面最大** | 主机是转发枢纽：主机自己改的值同样经 relay 分发；且主机存档即最终存档 | 【推断】高 |
| **F5** | **一致性间接暴露** | 为了让数值生效常伴随位移/传送；`MsgPlayerState` 有 `teleportCounter`、`qPosX/qPosY` 量化与 `ShouldSendOwnState` 节流。异常位姿会在他人视角表现为橡皮筋/穿墙 | 【推断】中 |
| **F6** | **社交/管理后果** | `NetKickReason` 有 `Kicked`（手动踢）与 `Banned`；主机可手动踢人。无自动判定，但**人工判定成本极低**（F1 可见） | 【事实-程序集】+【推断】 |
| **F7** | **无服务端兜底，但也没有"安全网"** | PlayFab 不参与玩法权威（1.5）→ 没有第三方审计，但也意味着**没有任何机制会替你纠正错误值**，坏状态会一直被当作真相 | 【推断】 |

### 5.4 一句话结论

> 在联机下修改玩家状态，**不是"会被主机覆盖"，也不是"会被哈希判定作弊"，而是"你的假数值会被当真值传播、被写进存档、并让其它系统的缓存与之矛盾"**。
> 对模组而言这比"被覆盖"更危险：**没有自动纠正机制，破坏是持久的**。

---

## 6. 门控设计建议

### 6.1 策略（第一版定稿）

**检测到联机 → 全部状态修改功能硬禁用。不做"仅主机"模式。**

理由：

1. 主机同样在联机中，其改动会经 relay 广播并写入最终存档（F4），风险不亚于客户端；
2. "仅主机"需要区分三态并维护两套语义，第一版没有收益；
3. 本模组定位是**单机生存助手**。

### 6.2 硬禁用的正确含义

"硬禁用" = **在功能实现层禁止写入**，而不是仅隐藏 UI。

```csharp
internal static class NetGate
{
    internal static bool IsMultiplayer() { /* 见 4.2 */ }

    // 唯一正确的守卫形态：在写入路径的最前端直接返回
    internal static bool BlockIfMultiplayer(string feature)
    {
        if (!IsMultiplayer()) return false;
        // 只记录，不修改任何游戏状态
        MPLog.Once(feature);
        return true;
    }
}
```

在所有写入入口的**第一行**：

```csharp
if (NetGate.BlockIfMultiplayer("SetHunger")) return;   // 直接返回，不进入任何写逻辑
```

需要覆盖的写入入口（均为【事实-程序集】确认存在的 public setter）—— 它们都在 `BasicCharacterController` 上：

```
set_hp(float)            set_energy(float)      set_casEnergy(float)
set_fatigue(float)       set_hunger(float)      set_thirst(float)
set_sanity(float)
```

> `set_hp` 为 `public virtual`（`set_hp_Public_Virtual_New_set_Void_Single_0`），说明存在子类覆盖；守卫必须放在**你自己的调用点**，不要 patch 这些 setter 本体。

### 6.3 禁用时"不回写、不重置"的具体规则

这是最容易做错的地方。**禁止以下四种行为**：

| 禁止 | 为什么 |
|---|---|
| ❌ 先快照、禁用时还原（snapshot/restore） | 还原本身就是一次写入，且可能把网络刚下发的合法值（如死亡/复活后的 hp）覆盖回旧值 |
| ❌ 禁用时 `Clamp` / 归一化 | `Clamp` 也是写操作；联机下等于篡改 |
| ❌ 在 `else` 分支"恢复原值" | 等价于上面两条 |
| ❌ 卸载 Mod / 关闭功能时把数值改回去 | 会把主机同步下来的真实值冲掉 |

**正确做法：**

- 禁用路径**只读不写**：判断 → 记日志 → `return`，不触碰任何 `set_*`。
- 不注册任何 Harmony patch 到 `BasicCharacterController` 的 status setter；守卫放在**功能函数入口**，从根上不产生写入调用。
- UI 可以显示"联机模式：已禁用"，但**不要**为了让 UI 一致而回写状态。
- 读操作（显示当前饥饿/口渴）在联机下也建议关闭或标注为"仅供参考"，因为多人下数值本就不同步（2.2），展示出来会误导。

### 6.4 建议的守卫覆盖点（按功能）

| 功能 | 守卫位置 | 禁用后行为 |
|---|---|---|
| 设置饥饿/口渴/疲劳/精力/理智 | 各设置函数首行 | 直接 return，UI 置灰并提示 |
| 设置 hp | 同上 | 直接 return |
| 无限饥饿/口渴（周期回写） | 定时器 tick 首行，**且不注册定时器** | 不注册即无回写 |
| Buff 施加 | 施加函数首行 | return |
| 物品/弹药增减 | 库存写入入口 | return（另需注意 `MsgSharedInv*` 共享背包） |
| 传送/坐标 | 移动入口 | return |
| 世界时间/存档 | 写入入口 | return（`allowLocalWorldSave` 客户端本就 false） |

### 6.5 附加建议

1. **日志只记一次**：联机下每次 tick 都写日志会刷爆 `Player.log`。
2. **不要改 `NetSim.allowLocal*`**（3.5）。
3. **不要 patch 网络同步方法**（`NetPlayerSync.*`、`NetMonsterSync.*`、`NetStateAudit.*`）—— 会直接破坏同步不变式。
4. 若将来要做"仅主机"，须先把 4.2 的判定改成三态（`Offline/Host/Client`），并逐功能重新评估 F4。

---

## 7. 联机相关风险表

| 功能 | 联机风险 | 机制原因 | 建议策略 |
|---|---|---|---|
| 设置饥饿 | 高（持久污染） | `hunger` 无周期同步（2.2），改动不被覆盖；随 `MsgPlayerSpawn.characterDataBytes` 在重生/重连时传播并可能被主机存档 | 联机硬禁用 |
| 设置口渴 | 高（持久污染） | 同上（`thirst`） | 联机硬禁用 |
| 设置疲劳 | 高（缓存分叉） | `fatigue` 本地推进，`UpdateStatus()` 按缓存计算；改后与其他派生值矛盾 | 联机硬禁用 |
| 设置精力 | 高（缓存分叉） | `energy` / `casEnergy` 均有独立 setter，且参与技能消耗判定 | 联机硬禁用 |
| 设置理智 | 高（持久污染） | `sanity` 同上 | 联机硬禁用 |
| 设置 hp | **极高** | `MsgPlayerState.hp` 自报 → 主机 relay → 他人 `ApplyPuppetHp` 显示你的假血量（F1）；同时死亡/倒地由网络下发，自报 hp 与之冲突 | 联机硬禁用 |
| 无限饥饿/口渴（周期回写） | 极高 | 每 tick 写入会持续经 `MsgPlayerState` 广播，异常最显眼 | 联机不注册定时器 |
| 施加/清除 Buff | 高 | `MsgPlayerBuff` / `MsgBuffFx` 按 `buffId,buffEndTime` 同步；`NetApplyVisualBuff` 会覆盖视觉表现 | 联机硬禁用 |
| 修改物品/弹药数量 | 极高 | `NetStateAudit` 审核 **`itemIds`** 集合（`itemMissing`/`itemExtra`），且 `NetSharedInventory` 有主机侧 `HostSession` 副本 | 联机硬禁用；**且此项确实会被审核发现** |
| 修改共享背包 | 极高 | `MsgSharedInvOp/Ops/Snapshot`，主机持有 `HostSession` 基准 | 联机硬禁用 |
| 生成物品到地面 | 极高 | `allowLocalGroundItemSpawn` 客户端为 false；私刷会表现为 `itemExtra` | 联机硬禁用 |
| 传送/改坐标 | 极高 | `MsgPlayerState` 有 `teleportCounter` + `qPosX/qPosY` 量化；异常位移在他人视角为橡皮筋 | 联机硬禁用 |
| 修改怪物/生成怪物 | 极高 | 怪物**主机权威**（`NetMonsterSync.Host*`），审核域含 `monsterIds`（`monsterMissing/monsterExtra`） | 联机硬禁用 |
| 修改载具 | 极高 | `MsgVehicle*` 全套为 `Request/Ack/Reply`；审核域含 `vehicleIds` | 联机硬禁用 |
| 修改地形/建造 | 极高 | `MsgTerrain*`/`MsgVoxel*` 走 `Request→Result`，且审核对区块/地块/土壤做哈希（`ComputeTileHash`/`ComputeSoilHash`） | 联机硬禁用 |
| 修改世界时间 | 高 | `MsgWorldClock` + 审核 `clockDelta` | 联机硬禁用 |
| 存档写入 | 高 | `allowLocalWorldSave` 客户端为 false；主机存档会把污染值固化 | 联机硬禁用 |
| **只读显示**（血量/饥饿数值面板） | 中 | 不写入，无同步风险；但多人下数值本就不同步，展示会误导 | 允许，但标注"仅供参考 / 联机未验证" |
| 修改 `NetSim.allowLocal*` | **灾难级** | 破坏"客户端不产生权威数据"不变式（3.5）；且无 setter，需伪造 | **绝对禁止** |
| patch 网络同步器 | **灾难级** | 直接破坏 `NetPlayerSync`/`NetMonsterSync`/`NetStateAudit` 一致性 | **绝对禁止** |

---

## 8. 技术记录表

| 名称 | 类型 | 用途 | 访问方式 | 修改方式 | 风险 | 测试 |
|---|---|---|---|---|---|---|
| `ZedZone.Net.NetSim` | `public static class` | 联机模拟权限外观类 | 类型名直接访问（全静态） | 不可实例化 | 只读使用安全 | 未实机验证 |
| `NetSim.isSimulationAuthority` | `public static bool { get; }` | 本机是否为世界模拟方 | `NetSim.isSimulationAuthority` | **无 setter，不可写** | 误当作"是否联机"会在主机放行 | 未实机验证 |
| `NetSim.allowLocalCharacterSpawn` | `public static bool { get; }` | 是否允许本地生成角色 | 同上 | 无 setter | 伪造将破坏生成一致性 | 未实机验证 |
| `NetSim.allowLocalWorldSave` | `public static bool { get; }` | 是否允许本地存档 | 同上 | 无 setter | 伪造导致双份权威存档 | 未实机验证 |
| `NetSim.allowLocalGroundItemSpawn` | `public static bool { get; }` | 是否允许本地刷地面物品 | 同上 | 无 setter | 伪造触发 `itemExtra` | 未实机验证 |
| `NetSim.allowLocalLootSpawn` | `public static bool { get; }` | 是否允许本地刷战利品 | 同上 | 无 setter | 同上 | 未实机验证 |
| `NetSim.allowLocalVehicleSpawn` | `public static bool { get; }` | 是否允许本地刷载具 | 同上 | 无 setter | 触发 `vehicleExtra` | 未实机验证 |
| `NetSim.allowGameplayPause` | `public static bool { get; }` | 是否允许本地暂停 | 同上 | 无 setter | 低；`[CallerCount(0)]` 疑似未引用 | 未实机验证 |
| `NetSim.advanceWorldEvents` | `public static bool { get; }` | 是否本地推进世界事件 | 同上 | 无 setter | 低（只读参考） | 未实机验证 |
| `NetSim.realtimeWorkProgress` | `public static bool { get; }` | 生产是否本地实时推进 | 同上 | 无 setter | 低 | 未实机验证 |
| `NetSim.singlePetSession` | `public static bool { get; }` | 单例宠物会话标志 | 同上 | 无 setter | 低 | 未实机验证 |
| `NetSim.guestWorldGameData` | `public static GameData` 字段 | 访客世界数据 | 读写均可 | **可赋值（唯一可写成员）** | **高**：指向访客副本，写入不落本地存档 | 未实机验证 |
| `NetworkManager.instance` | `public static NetworkManager` | 网络管理单例 | `NetworkManager.instance` | 不建议 | 低 | 未实机验证 |
| `NetworkManager.isOnline` | `public static bool { get; }` | 是否联机 | `NetworkManager.isOnline` | 无 setter | 低；**推荐主判据** | 未实机验证 |
| `NetworkManager.isHost` | `public static bool { get; }` | 是否主机 | 同上 | 无 setter | 低 | 未实机验证 |
| `NetworkManager.isClient` | `public static bool { get; }` | 是否客户端 | 同上 | 无 setter | 低 | 未实机验证 |
| `NetworkManager.mode` | `NetMode { get; }`（实例） | 三态：Offline/Host/Client | `instance.mode` | 无 setter | 低；**推荐判据** | 未实机验证 |
| `NetworkManager.clientState` | `NetClientState { get; }`（实例） | 客户端握手状态 | `instance.clientState` | 无 setter | 低；主机侧无意义 | 未实机验证 |
| `NetworkManager.worldMessagesAllowed` | `public static bool { get; }` | 世界消息闸门 | 直接访问 | 无 setter | 低 | 未实机验证 |
| `NetworkManager.localPlayerId` | `int { get; }`（实例） | 本地玩家 id | `instance.localPlayerId` | 无 setter | 低 | 未实机验证 |
| `NetPlayerSync.remotePlayerCount` | `public static int { get; }` | 远端玩家数 | 直接访问 | 无 setter | 主机独占房间时为 0，不能判联机 | 未实机验证 |
| `NetPlayerSync.worldReady` | `public static bool { get; }` | 世界就绪 | 直接访问 | 无 setter | 低 | 未实机验证 |
| `NetPlayerSync.worldTerrainReady` | `public static bool { get; }` | 地形就绪 | 直接访问 | 无 setter | 低 | 未实机验证 |
| `NetPlayerSync.IsPlayerControlled` | `public bool (BasicCharacterController)` | 控制器是否由玩家控制 | 实例方法 | — | 只读 | 未实机验证 |
| `NetPlayerSync.TryGetPlayerIdOf` | `public bool (BasicCharacterController, out int)` | 反查玩家 id | 实例方法 | — | 只读 | 未实机验证 |
| `MsgPlayerState.hp` | `float` | 自报 hp | 消息字段 | 发送侧自报 | **高**：见 F1 | 未实机验证 |
| `MsgPlayerState.qHp` | `uint` | hp 量化副本 | 消息字段 | 随 hp 派生 | 高 | 未实机验证 |
| `MsgPlayerState.teleportCounter` | `uint` | 传送计数（抑制误判） | 消息字段 | — | 篡改会导致位姿异常 | 未实机验证 |
| `MsgPlayerStateBatch.states` | 集合 | 批量状态中继 | 消息字段 | — | 只读 | 未实机验证 |
| `MsgPlayerBuff` | `buffId`, `buffEndTime` | buff 同步 | 消息字段 | — | 篡改造成他人视觉不一致 | 未实机验证 |
| `MsgPlayerDowned` | `playerId, downed, remainMs, hpPercentOnRevive, died, posX, posY, zLevel, rescuerPlayerIdPlusOne` | 倒地/救援状态 | 消息字段 | — | 高 | 未实机验证 |
| `MsgPlayerDeath` | `playerId, posX, posY, zLevel, corpseConfigStr, corpseItemsBytes, standVz` | 死亡与尸体背包快照 | 消息字段 | — | 高 | 未实机验证 |
| `MsgPlayerRespawn` | `playerId, posX, posY, zLevel, spawnSource, movedAshore` | 复活 | 消息字段 | — | 高 | 未实机验证 |
| `MsgPlayerSpawn.characterDataBytes` | `byte[]` | 生成时全量 `CharacterData` | 消息字段 | — | **极高**：含全部要害数值 | 未实机验证 |
| `MsgPlayerJoinWorld.characterDataBytes` | `byte[]` | 加入世界时全量 `CharacterData` | 消息字段 | — | 极高 | 未实机验证 |
| `MsgPlayerHitRequest.damage` | `float` | 玩家受击请求伤害 | 消息字段 | 请求式 | 高（权威端裁决） | 未实机验证 |
| `CharacterData.characterStatusData` | `CharacterStatusData` | 要害数值容器 | 字段 | 直接赋值（单机） | **极高**：联机下随 spawn 传播 | 未实机验证 |
| `CharacterStatusData.hp` | `float` | 当前血量 | 字段 | 直接赋值 | 极高（配合 `maxhp`） | 未实机验证 |
| `CharacterStatusData.hunger` | `float` | 饥饿 | 字段 | 直接赋值 | 高（持久污染） | 未实机验证 |
| `CharacterStatusData.thirst` | `float` | 口渴 | 字段 | 直接赋值 | 高 | 未实机验证 |
| `CharacterStatusData.fatigue` | `float` | 疲劳 | 字段 | 直接赋值 | 高 | 未实机验证 |
| `CharacterStatusData.energy` | `float` | 精力 | 字段 | 直接赋值 | 高 | 未实机验证 |
| `CharacterStatusData.sanity` | `float` | 理智 | 字段 | 直接赋值 | 高 | 未实机验证 |
| `CharacterStatusData.bloodVolume` | `float` | 血量体积 | 字段 | 直接赋值 | 高 | 未实机验证 |
| `CharacterStatusData.m_bodyTemperature` | `float` | 体温 | 字段 | 直接赋值 | 中高 | 未实机验证 |
| `CharacterStatusData.radiationDose` | `float` | 辐射剂量 | 字段 | 直接赋值 | 中高 | 未实机验证 |
| `CharacterStatusData.maxhp` 等 `max*` | `float` | 各数值上限 | 字段 | 直接赋值 | 高：上限改了会与派生缓存矛盾（F3） | 未实机验证 |
| `BasicCharacterController.set_hunger` | `public void (float)` | 饥饿写入入口 | 实例方法 | 调用即写 | 高 | 未实机验证 |
| `BasicCharacterController.set_thirst` | `public void (float)` | 口渴写入入口 | 实例方法 | 调用即写 | 高 | 未实机验证 |
| `BasicCharacterController.set_fatigue` | `public void (float)` | 疲劳写入入口 | 实例方法 | 调用即写 | 高 | 未实机验证 |
| `BasicCharacterController.set_energy` | `public void (float)` | 精力写入入口 | 实例方法 | 调用即写 | 高 | 未实机验证 |
| `BasicCharacterController.set_casEnergy` | `public void (float)` | 特殊精力写入入口 | 实例方法 | 调用即写 | 高 | 未实机验证 |
| `BasicCharacterController.set_sanity` | `public void (float)` | 理智写入入口 | 实例方法 | 调用即写 | 高 | 未实机验证 |
| `BasicCharacterController.set_hp` | `public virtual void (float)` | 血量写入入口 | 实例方法 | 调用即写 | **极高** | 未实机验证 |
| `BasicCharacterController.UpdateStatus` | `protected virtual void ()` | 每帧状态推进 | 虚方法 | 不建议 patch | 中（patch 易引入不一致） | 未实机验证 |
| `BasicCharacterController.staminaRegenScale` | `float { get; }` | 耐力回复系数 | 属性 | 无 setter | 低 | 未实机验证 |
| `HumanCharacterController.NetApplyLocalDeath` | `public Vector2 (Vector2, out int, out string, out InventoryData)` | 网络下发本地死亡 | 实例方法 | 不建议 | 高：与自改状态冲突 | 未实机验证 |
| `HumanCharacterController.NetApplyLocalRespawn` | `public void (Vector2)` | 网络下发本地复活 | 实例方法 | 不建议 | 高 | 未实机验证 |
| `HumanCharacterController.NetEnterDowned` | `public void ()` | 网络下发倒地 | 实例方法 | 不建议 | 高 | 未实机验证 |
| `HumanCharacterController.NetApplyVomit` | `public void (bool)` | 网络下发呕吐 | 实例方法 | 不建议 | 中 | 未实机验证 |
| `HumanCharacterController.NetSetBodyHidden` | `public void (bool)` | 网络下发隐藏身体 | 实例方法 | 不建议 | 中 | 未实机验证 |
| `BasicCharacterController.NetApplyVisualBuff` | `public void (int, float)` | 网络下发 buff 视觉 | 实例方法 | 不建议 | 中 | 未实机验证 |
| `NetStateAudit.TriggerNow` | `public static void ()` | 立即触发审核 | 静态方法 | 可调用 | 中：人为触发增加主机负担/暴露 | 未实机验证 |
| `NetStateAudit.instance` | `public static NetStateAudit { get; }` | 审核单例 | 静态属性 | 无 setter | 低 | 未实机验证 |
| `NetStateAudit.lastSummary` | `public static string { get; }` | 最近审核摘要 | 静态属性 | 无 setter | 低（可作只读诊断） | 未实机验证 |
| `MsgAuditReport.itemIds` | 集合 | 审核物品集合 | 消息字段 | — | 高：物品篡改在此暴露 | 未实机验证 |
| `MsgAuditReport.monsterIds` | 集合 | 审核怪物集合 | 消息字段 | — | 高 | 未实机验证 |
| `MsgAuditReport.vehicleIds` | 集合 | 审核载具集合 | 消息字段 | — | 高 | 未实机验证 |
| `MsgAuditReply.mismatches` | 计数 | 哈希不一致数 | 消息字段 | — | 只读 | 未实机验证 |
| `NetKickReason` | `enum` | 踢出原因 | 类型 | — | **无 Cheat/Desync 项** → 无数值异常自动踢 | 未实机验证 |
| `NetMode` | `enum { Offline, Host, Client }` | 联机三态 | 类型 | — | 低 | 未实机验证 |
| `NetClientState` | `enum { Disconnected, Connecting, Handshaking, Joined }` | 客户端握手状态 | 类型 | — | 低 | 未实机验证 |
| `MsgSessionConfig.maxPlayers` | `int` | 会话人数上限（主机配置） | 消息字段 | 主机侧设定 | 中 | 未实机验证 |
| `MsgSessionConfig.friendlyFire` | `bool` | 友伤开关 | 消息字段 | 主机侧设定 | 中 | 未实机验证 |
| `CoopBetaGate.IsWhitelisted` | `public static bool (ulong)` | beta 白名单校验 | 静态方法 | — | 只读 | 未实机验证 |
| `CoopBetaGate.isReady` | `public static bool { get; }` | 白名单就绪 | 静态属性 | — | 只读；与"是否联机"无关 | 未实机验证 |
| `NetworkManager.BetaCoopBlocked` | `public static bool (string)` | beta 合作拦截判定 | 静态方法 | — | 只读 | 未实机验证 |

---

## 9. 无法确认的清单

### 9.1 硬性缺失：调用关系无法静态解析

| 事项 | 状态 | 缺什么 |
|---|---|---|
| **具体哪些类型/方法读取 `isSimulationAuthority`** | 【未验证】 | interop 无方法体、无调用点；`MethodXrefScanCache.db` 仅存数字 id（已实测无方法名字符串），无法反解；游戏目录无 IL2CPP dump（无 `dump.cs`/`il2cpp_dump.json`） |
| `isSimulationAuthority` 的**实际计算表达式** | 【未验证】 | 需读 getter 方法体（`il2cpp_runtime_invoke` 跳板不可见） |
| `allowLocal*` 各属性的具体真假条件 | 【未验证】 | 同上 |

### 9.2 行为层未确认

| 事项 | 状态 | 备注 |
|---|---|---|
| **主机是否对客户端自报的 `hp` 做任何校验/夹取** | 【未验证】 | 关键！`OnHostReceiveState` 方法体不可见。若主机有校验，F1 的严重性下降；若无校验则本文判断成立。**建议 Phase-2 优先验证** |
| `OnHostReceiveState` 收到 hp 后是否也更新主机本地显示 | 【推断】 | 依 `ApplyPuppetHp` 命名推断"仅作用于影子" |
| `NetStateAudit` 不一致时主机是否**自动**采取动作（踢/记日志/忽略） | 【未验证】 | `OnHostReceiveReport`/`OnClientReceiveReply` 体不可见；`NetKickReason` 无对应枚举项，**间接倾向"无自动踢"** |
| `NetStateAudit` 的 `autoIntervalSeconds` 实际取值与默认开启状态 | 【未验证】 | 字段存在，值不可读 |
| 审核是否也覆盖 `zLevel`/岛屿切换（`MsgIslandTravel`） | 【未验证】 | 由 `DomainsToText(int)` 暗示存在"域"位掩码，但域定义不可见 |
| `MsgPlayerHitRequest` 的权威裁决方（主机 or 受击方）与 `friendlyReduced` 的具体算法 | 【未验证】 | 仅有字段 |
| `guestWorldGameData` 在客户端上何时被赋值、指向什么 | 【推断】 | 推断为"主机下发的访客世界 GameData"；有 setter，赋值时机不可见 |
| 客户端本地是否有**独立**的 `CharacterData`（与访客世界不同步） | 【未验证】 | 直接决定"改本地数值是否完全无效" |
| `hp` 在客户端被 `NetApplyLocalDeath` 等影响的精确边界 | 【未验证】 | 方法体不可见 |
| 联机下 `UpdateStatus()` 是否仍按本地时钟推进 hunger/thirst | 【推断】 | `realtimeWorkProgress`/`advanceWorldEvents` 暗示有区分，但玩家 status 是否例外未确认 |

### 9.3 数值/版本类

| 事项 | 状态 | 缺什么 |
|---|---|---|
| 8 人上限 | 【未验证】 | `MsgSessionConfig.maxPlayers` 存在，但默认值/上限不可见；`changelog.txt` 是 BepInEx 的，非游戏日志 |
| 「1.0, 2026-11-06」版本与联机上线时间 | 【官方公告】未采集 | 本次未做联网检索，且游戏内无可读版本公告文件。需另取官方渠道 |
| 联机是否仍处于 beta 白名单阶段 | 【推断】 | `CoopBetaGate`/`CoopBetaWatermark`/`BetaCoopBlocked` 强烈暗示存在 beta 闸门，但当前是否已放开未知 |

### 9.4 日志类

| 事项 | 状态 | 缺什么 |
|---|---|---|
| 真实联机栈轨迹 | **不存在** | 两份日志均为单机启动（0 处 `ZedZone.Net`）。任务简报的前提有误，**需一次真实联机会话的 `Player.log`** 才能取得 `【事实-日志】` 级证据 |

> 若要补齐 9.1/9.2 的关键项，最有效路径是：**一次真实联机会话的 `Player.log`**（可验证 `isOnline`/`mode` 的实际取值、审核是否触发、是否有异常栈），其次才是更重的 IL2CPP 反编译。

---

## 10. 给实现层的一页纸结论

1. **判定联机**：用 `NetworkManager.isOnline`，兜底 `NetworkManager.instance.mode != NetMode.Offline`；**每次实时求值，不缓存**。
2. **不要**用 `NetSim.isSimulationAuthority` 判联机 —— 主机上它为 `true`。
3. **不要**写 `NetSim.allowLocal*` —— 无 setter，且会破坏一致性不变式。
4. **硬禁用**：在所有写入入口第一行 `return`，**只读不写**，禁止快照/还原/Clamp。
5. **风险认知**：联机改数值 **不会**被主机覆盖、**不会**被哈希审核发现，但会**传播给所有人、写进存档、并与派生缓存矛盾** —— 这是持久破坏，比"被覆盖"更糟。
6. 第一版：**联机一律禁用，不做仅主机模式**。

---

*本文为 Phase-1 静态分析产物。所有行为性结论均标【推断】，未经实机验证。*
