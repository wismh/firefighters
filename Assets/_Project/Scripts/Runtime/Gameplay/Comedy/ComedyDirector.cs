using System.Collections.Generic;
using _Project.Audio;
using _Project.City;
using _Project.Economy;
using _Project.Fire;
using _Project.FireHose;
using _Project.Hazards;
using _Project.Hud;
using _Project.Mission;
using _Project.Pedestrians;
using _Project.Player;
using _Project.Traffic;
using _Project.Vehicles;
using MyToolkit.ParticleFx;
using UnityEngine;
using Zenject;

namespace _Project.Comedy
{
    /// <summary>
    /// The slapstick layer over the city. Listens to what the player does — crashes, props knocked over, traffic
    /// cars knocked out, drivers sprayed with the hose, buildings saved — and answers with comic words, emoji over
    /// the drivers, screen shake and freeze-frames, money flying out of what broke, and puts it all on the bill.
    /// It only reacts: the physics of geysers and water live in their own systems.
    /// </summary>
    public sealed class ComedyDirector : MonoBehaviour
    {
        private const float BigTextCooldown = 0.35f;
        private const float SoakCooldown = 1.6f;
        private const float AirTimeForCheer = 0.7f;
        private const int FinaleFireworks = 9;
        private const float FinaleSpacing = 0.45f;
        // Below this speed the player's car was hit rather than hitting.
        private const float RammedSpeed = 3f;
        private const float ScaredLineCooldown = 0.6f;
        private const float ScreamCooldown = 0.9f;
        private const float BurningLineInterval = 2.2f;
        private const float BurningShakeInterval = 0.25f;

        private static readonly ComedyFxId[] SoakedFaces =
        {
            ComedyFxId.EmojiCry, ComedyFxId.EmojiTeary, ComedyFxId.EmojiDerp, ComedyFxId.EmojiClenchTeeth,
            ComedyFxId.EmojiDisappointed,
        };

        private static readonly ComedyFxId[] KnockedFaces =
        {
            ComedyFxId.EmojiAngry, ComedyFxId.EmojiShocked, ComedyFxId.EmojiNervous,
        };

        private static readonly ComedyFxId[] CrashWords = { ComedyFxId.TextPow, ComedyFxId.TextKaPow, ComedyFxId.TextCrack };
        private static readonly ComedyFxId[] Fireworks =
            { ComedyFxId.FireworkRed, ComedyFxId.FireworkBlue, ComedyFxId.FireworkYellow };

        private static readonly Color MoneyColor = new Color(1f, 0.35f, 0.3f);
        private static readonly Color GoodColor = new Color(0.45f, 1f, 0.45f);
        private static readonly Color GhostColor = new Color(0.75f, 0.9f, 1f);
        private static readonly Color ShoutColor = new Color(1f, 0.95f, 0.55f);

        [SerializeField] private Camera viewCamera;
        [SerializeField] private ComedyScript script;
        [SerializeField] private ComedySounds sounds = new ComedySounds();

        [Header("Crashes")]
        [Tooltip("Dent depth, metres, from which a crash gets a comic word, a freeze-frame and a big shake.")]
        [SerializeField] private float bigCrashDepth = 0.12f;
        [SerializeField] private float hitStopSeconds = 0.07f;

        private readonly Dictionary<Vehicle, float> _nextSoak = new Dictionary<Vehicle, float>(16);
        private readonly HashSet<Vehicle> _soaked = new HashSet<Vehicle>();

        private IParticleFxSpawnService<ComedyFxId> _fx;
        private SpatialSfx _sfx;
        private TrafficSystem _traffic;
        private FireService _fires;
        private WaterNozzle _nozzle;
        private List<KnockableProp> _props;
        private PedestrianSystem _pedestrians;
        private BurningView _burning;
        private DamageBill _bill;
        private MissionDirector _mission;
        private IPlayerState _player;
        private FirstPersonController _walker;
        private HydrantGeysers _geysers;
        private CameraFeel _feel;
        private FloatingTextView _texts;

        private Vehicle _driven;
        private float _lastBigText = float.NegativeInfinity;
        private bool _cheered;
        private int _finaleLeft;
        private float _finaleTimer;
        private float _nextBurningLine;
        private float _nextBurningShake;
        private float _nextScaredLine;
        private float _nextScream;

        [Inject]
        public void Construct(IParticleFxSpawnService<ComedyFxId> fx, SpatialSfx sfx, TrafficSystem traffic,
            FireService fires, WaterNozzle nozzle, List<KnockableProp> props, PedestrianSystem pedestrians,
            BurningView burning, DamageBill bill, MissionDirector mission, IPlayerState player,
            FirstPersonController walker, HydrantGeysers geysers, CameraFeel feel, FloatingTextView texts)
        {
            _fx = fx;
            _sfx = sfx;
            _traffic = traffic;
            _fires = fires;
            _nozzle = nozzle;
            _props = props;
            _pedestrians = pedestrians;
            _burning = burning;
            _bill = bill;
            _mission = mission;
            _player = player;
            _walker = walker;
            _geysers = geysers;
            _feel = feel;
            _texts = texts;
        }

        private void Start()
        {
            foreach (var prop in _props)
            {
                prop.Knocked += OnPropKnocked;
            }

            _traffic.KnockedOut += OnKnockedOut;
            _fires.BuildingSaved += OnBuildingSaved;
            _pedestrians.Hit += OnPedestrianHit;
            _pedestrians.Scared += OnPedestrianScared;
            _pedestrians.Soaked += OnPedestrianSoaked;
            _pedestrians.GhostAppeared += OnGhostAppeared;
            _burning.CaughtFire += OnCaughtFire;
            _geysers.Opened += OnGeyserOpened;
            _mission.AllFiresOut += OnAllFiresOut;
            _player.JumpedOut += OnJumpedOut;
            _player.VehicleEntered += OnVehicleEntered;
            _player.VehicleExited += OnVehicleExited;
            if (_player.Driven != null)
            {
                OnVehicleEntered(_player.Driven);
            }
        }

        private void OnDestroy()
        {
            foreach (var prop in _props)
            {
                prop.Knocked -= OnPropKnocked;
            }

            _traffic.KnockedOut -= OnKnockedOut;
            _fires.BuildingSaved -= OnBuildingSaved;
            _pedestrians.Hit -= OnPedestrianHit;
            _pedestrians.Scared -= OnPedestrianScared;
            _pedestrians.Soaked -= OnPedestrianSoaked;
            _pedestrians.GhostAppeared -= OnGhostAppeared;
            _burning.CaughtFire -= OnCaughtFire;
            _geysers.Opened -= OnGeyserOpened;
            _mission.AllFiresOut -= OnAllFiresOut;
            _player.JumpedOut -= OnJumpedOut;
            _player.VehicleEntered -= OnVehicleEntered;
            _player.VehicleExited -= OnVehicleExited;
            if (_driven != null)
            {
                _driven.Crashed -= OnCrashed;
            }
        }

        private void OnVehicleEntered(Vehicle vehicle)
        {
            if (_driven == vehicle)
            {
                return;
            }

            if (_driven != null)
            {
                _driven.Crashed -= OnCrashed;
            }

            _driven = vehicle;
            _driven.Crashed += OnCrashed;
        }

        private void OnVehicleExited(Vehicle vehicle)
        {
            // A runaway may stop after the player has taken another car.
            if (vehicle != _driven)
            {
                return;
            }

            vehicle.Crashed -= OnCrashed;
            _driven = null;
        }

        private void OnCrashed(Vehicle vehicle, Vector3 point, float depth)
        {
            var cost = _bill.AddDent(depth);
            var big = depth >= bigCrashDepth;
            if (!big)
            {
                _feel.Shake(0.15f + depth * 2f);
                _sfx.Play(sounds.smallCrash, point, 0.7f);
                return;
            }

            _feel.Shake(0.45f + depth);
            _feel.HitStop(hitStopSeconds);
            _sfx.Play(sounds.bigCrash, point);
            _sfx.Play(sounds.bigCrashThud, point, 0.8f);
            _fx.SpawnParticle(Random.value < 0.5f ? ComedyFxId.Punch : ComedyFxId.ToonPunch, point);
            if (Time.unscaledTime - _lastBigText > BigTextCooldown)
            {
                _lastBigText = Time.unscaledTime;
                _fx.SpawnParticle(Pick(CrashWords), point + Vector3.up * 3.5f);
            }

            _texts.Add(point + Vector3.up * 5.5f, Money(cost), MoneyColor);
        }

        private void OnPropKnocked(KnockableProp prop)
        {
            var kind = prop.Kind;
            var cost = _bill.AddProp(kind);
            var position = prop.transform.position;
            _fx.SpawnParticle(ComedyFxId.DustPoof, position + Vector3.up * 0.3f);
            foreach (var reaction in script.propFx)
            {
                if (reaction.kind == kind)
                {
                    _fx.SpawnParticle(reaction.fx, position + Vector3.up * reaction.height);
                }
            }

            if (cost >= script.expensiveProp)
            {
                _fx.SpawnParticle(ComedyFxId.DollarBlast, position + Vector3.up * 1.5f);
                _sfx.Play(sounds.moneyDrop, position);
            }

            _feel.Shake(0.12f);
            _sfx.Play(sounds.propDown, position);
            _texts.Add(position + Vector3.up * 8f, Money(cost), MoneyColor);
        }

        private void OnGeyserOpened(Vector3 position)
        {
            _fx.SpawnParticle(ComedyFxId.Geyser, position);
            _fx.SpawnParticle(ComedyFxId.TextKaPow, position + Vector3.up * 5f);
            _sfx.Play(sounds.geyser, position);
            _texts.Add(position + Vector3.up * 9f, script.geyser, ShoutColor, 1.2f);
        }

        private void OnKnockedOut(Vehicle vehicle)
        {
            var roof = vehicle.CameraTarget.position;
            _fx.SpawnParticle(ComedyFxId.StunStars, roof + Vector3.up * 0.6f);
            _fx.SpawnParticle(Pick(KnockedFaces), roof + Vector3.up * 2.6f);
            _sfx.Play(sounds.knockout, roof);
            _sfx.Play(sounds.driverKnocked, roof);
            _texts.Add(roof + Vector3.up * 4f, Pick(script.knocked), ShoutColor);
            // A traffic car that drove into the standing fire truck is its own driver's fault.
            if (_driven == null || _driven.PreStepVelocity.magnitude < RammedSpeed)
            {
                return;
            }

            var cost = _bill.AddKnockout();
            _texts.Add(roof + Vector3.up * 1.5f, Money(cost), MoneyColor, 0.8f);
        }

        private void OnJumpedOut(Vehicle vehicle, float speed)
        {
            var at = _walker.transform.position;
            _feel.Shake(Mathf.Clamp(speed * 0.04f, 0.3f, 0.8f));
            _sfx.Play(sounds.bigCrashThud, at, 0.7f);
            _sfx.Play(sounds.whoosh, at, 0.8f);
            _sfx.Play(sounds.playerThrown, at, 1f, true);
            _fx.SpawnParticle(ComedyFxId.DustPoof, at + Vector3.up * 0.3f);
            _texts.Add(AheadOfView(6f), Pick(script.jump), ShoutColor, 1.3f);
        }

        private void OnCaughtFire()
        {
            _sfx.Play(sounds.propDown, _walker.transform.position);
            _nextBurningLine = Time.time + BurningLineInterval;
            _sfx.Play(sounds.playerBurning, _walker.transform.position, 1f, true);
        }

        // The player in the flames complains and shakes until out of them.
        private void UpdateBurning()
        {
            if (!_burning.IsBurning)
            {
                return;
            }

            if (Time.time >= _nextBurningShake)
            {
                _nextBurningShake = Time.time + BurningShakeInterval;
                _feel.Shake(0.4f * _burning.Heat);
            }

            if (Time.time >= _nextBurningLine)
            {
                _nextBurningLine = Time.time + BurningLineInterval;
                _texts.Add(AheadOfView(5f), Pick(script.burning), ShoutColor, 1.3f);
                _sfx.Play(sounds.playerBurning, _walker.transform.position, 0.8f, true);
            }
        }

        private Vector3 AheadOfView(float distance)
        {
            return viewCamera.transform.position + viewCamera.transform.forward * distance;
        }

        private void OnPedestrianHit(Pedestrian pedestrian)
        {
            _bill.AddPedestrianHit();
            var position = pedestrian.Position;
            _feel.Shake(0.3f);
            _feel.HitStop(hitStopSeconds);
            _sfx.Play(sounds.bigCrashThud, position);
            _sfx.Play(sounds.pedestrianHit, position);
            _fx.SpawnParticle(ComedyFxId.Punch, position + Vector3.up);
            _fx.SpawnParticle(Pick(CrashWords), position + Vector3.up * 3.5f);
        }

        private void OnPedestrianScared(Pedestrian pedestrian)
        {
            if (Time.time >= _nextScream)
            {
                _nextScream = Time.time + ScreamCooldown * Random.Range(0.7f, 1.4f);
                _sfx.Play(sounds.pedestrianScream, pedestrian.Position);
            }

            if (Time.time < _nextScaredLine)
            {
                return;
            }

            _nextScaredLine = Time.time + ScaredLineCooldown;
            _texts.Add(pedestrian.HeadPosition + Vector3.up, Pick(script.scared), ShoutColor);
        }

        private void OnPedestrianSoaked(Pedestrian pedestrian)
        {
            _bill.AddSoakedPedestrian();
            _sfx.Play(sounds.driverSoaked, pedestrian.Position);
            _sfx.Play(sounds.pedestrianSoaked, pedestrian.Position);
            _texts.Add(pedestrian.HeadPosition + Vector3.up, Pick(script.soakedPedestrian), ShoutColor);
        }

        private void OnGhostAppeared(Ghost ghost)
        {
            var cost = _bill.AddMoralDamage();
            _sfx.Play(sounds.ghostLaugh, ghost.Top);
            _texts.Add(ghost.Top + Vector3.up * 2f, Pick(script.ghost), GhostColor, 1.1f);
            _texts.Add(ghost.Top + Vector3.up, Money(cost), MoneyColor);
        }

        private void OnBuildingSaved(BuildingFire fire)
        {
            _bill.AddSavedBuilding();
            var area = fire.Area;
            var top = new Vector3(area.center.x, area.max.y, area.center.z);
            _fx.SpawnParticle(ComedyFxId.Confetti, top);
            _fx.SpawnParticle(ComedyFxId.CoinBlast, top + Vector3.up);
            _sfx.Play(sounds.buildingSaved, top, 1f, true);
            _sfx.Play(sounds.coins, top, 0.8f, true);
            _sfx.Play(sounds.crowdCheer, top, 1f, true);
            _sfx.Play(sounds.crowdCheer, top, 0.8f, true);
            _texts.Add(top + Vector3.up * 5f, script.saved, GoodColor, 1.4f);
        }

        private void OnAllFiresOut()
        {
            _finaleLeft = FinaleFireworks;
            _finaleTimer = 1f;
            _sfx.Play(sounds.allSaved, _player.MapFocus.position, 1f, true);
        }

        private void Update()
        {
            UpdateCheer();
            UpdateSoaking();
            UpdateBurning();
            UpdateFinale(Time.deltaTime);
        }

        // A long flight in the player's car gets a whoop.
        private void UpdateCheer()
        {
            var airTime = _driven != null ? _driven.AirTime : 0f;
            if (airTime <= 0f)
            {
                _cheered = false;
                return;
            }

            if (_cheered || airTime < AirTimeForCheer)
            {
                return;
            }

            _cheered = true;
            var roof = _driven.CameraTarget.position;
            _sfx.Play(sounds.boing, roof, 0.7f, true);
            _sfx.Play(sounds.playerCheer, roof, 1f, true);
            _fx.SpawnParticle(ComedyFxId.EmojiCool, roof + Vector3.up * 2.6f);
            _texts.Add(roof + Vector3.up * 4.5f, Pick(script.flight), ShoutColor, 1.2f);
        }

        // A traffic driver caught in the stream sticks an unhappy face out and complains.
        private void UpdateSoaking()
        {
            if (!_nozzle.IsWaterArriving)
            {
                return;
            }

            var body = _nozzle.Hit.rigidbody;
            if (body == null || !body.TryGetComponent<Vehicle>(out var vehicle) || vehicle == _driven)
            {
                return;
            }

            if (_nextSoak.TryGetValue(vehicle, out var next) && Time.time < next)
            {
                return;
            }

            _nextSoak[vehicle] = Time.time + SoakCooldown;
            var roof = vehicle.CameraTarget.position;
            _sfx.Play(sounds.horn, roof, 0.6f);
            _sfx.Play(sounds.driverAngry, roof);
            _fx.SpawnParticle(Pick(SoakedFaces), roof + Vector3.up * 2.6f);
            _texts.Add(roof + Vector3.up * 4.5f, Pick(script.soaked), ShoutColor);
            if (_soaked.Add(vehicle))
            {
                _bill.AddSoakedDriver();
                _sfx.Play(sounds.driverSoaked, roof);
            }
        }

        private void UpdateFinale(float dt)
        {
            if (_finaleLeft <= 0)
            {
                return;
            }

            _finaleTimer -= dt;
            if (_finaleTimer > 0f)
            {
                return;
            }

            _finaleTimer = FinaleSpacing;
            _finaleLeft--;
            var around = Random.insideUnitCircle.normalized * Random.Range(14f, 28f);
            var focus = _player.MapFocus.position;
            var position = focus + new Vector3(around.x, Random.Range(16f, 26f), around.y);
            _fx.SpawnParticle(Pick(Fireworks), position);
            _sfx.Play(sounds.firework, position, 0.8f, true);
        }

        private string Money(int cost)
        {
            return string.Format(script.money, cost);
        }

        private static T Pick<T>(T[] items)
        {
            return items[Random.Range(0, items.Length)];
        }
    }
}
