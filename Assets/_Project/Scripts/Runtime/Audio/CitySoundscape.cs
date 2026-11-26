using _Project.FireHose;
using _Project.Player;
using _Project.Vehicles;
using MyToolkit.Audio;
using UnityEngine;
using Zenject;

namespace _Project.Audio
{
    /// <summary>
    /// The continuous sound of the city: the engine of the car the player drives, the fire truck's siren, tyre
    /// squeal, the hose and the steam where it hits flames, the roar of burning buildings near the listener, the
    /// evening ambience, footsteps, car doors, and the music — a chase theme at the wheel, a stroll on foot.
    /// </summary>
    public sealed class CitySoundscape : MonoBehaviour
    {
        private const float MaxCarSpeed = 28f;
        private const float Gears = 4f;
        private const float StrideLength = 1.9f;
        private const float HardLandingSpeed = -7f;
        private const float MinSkidSpeed = 7f;
        private const float SkidSidewaysSpeed = 4f;
        private const float SirenMinSpeed = 3f;

        [SerializeField] private PlayerModeController modes;
        [SerializeField] private FirstPersonController walker;
        [SerializeField] private Transform listener;

        [Header("Loops")]
        [SerializeField] private SoundData engine;
        [SerializeField] private SoundData siren;
        [SerializeField] private SoundData skid;
        [SerializeField] private SoundData hose;
        [SerializeField] private SoundData steam;
        [SerializeField] private SoundData fire;
        [SerializeField] private SoundData ambience;

        [Header("One-shots")]
        [SerializeField] private SoundBank footsteps;
        [SerializeField] private SoundData land;
        [SerializeField] private SoundData doorOpen;
        [SerializeField] private SoundData doorClose;

        [Header("Music")]
        [SerializeField] private SoundData driveMusic;
        [SerializeField] private SoundData walkMusic;
        [SerializeField] private float musicCrossfade = 2.5f;
        [Tooltip("Seconds on foot before the stroll theme takes over, so a quick hop out does not flip the music.")]
        [SerializeField] private float walkMusicDelay = 4f;

        [Header("Mix")]
        [SerializeField] private float fireHearingDistance = 55f;
        [SerializeField] private float sirenHearingDistance = 120f;
        [Tooltip("How fast loop volumes follow their targets, per second.")]
        [SerializeField] private float loopResponse = 6f;
        [Tooltip("Tyre squeal fades faster, so it stops with the slide.")]
        [SerializeField] private float skidResponse = 10f;

        private ILoopingSfxPlayer _loops;
        private ISfxPlayer _sfx;
        private IMusicPlayer _music;
        private FireService _fires;
        private WaterNozzle _nozzle;
        private BurningView _burning;

        private Loop _engine;
        private Loop _siren;
        private Loop _skid;
        private Loop _hose;
        private Loop _steam;
        private Loop _fire;
        private Loop _ambience;

        private float _stride;
        private float _airVelocity;
        private bool _wasGrounded = true;
        private float _onFootTime;

        /// <summary>A looping channel whose volume this component eases itself, so it can stop when silent.</summary>
        private struct Loop
        {
            public LoopingSfxHandle Handle;
            public SoundData Sound;
            public float Volume;
            public bool Playing;
        }

        [Inject]
        public void Construct(ILoopingSfxPlayer loops, ISfxPlayer sfx, IMusicPlayer music, FireService fires,
            WaterNozzle nozzle, BurningView burning)
        {
            _loops = loops;
            _sfx = sfx;
            _music = music;
            _fires = fires;
            _nozzle = nozzle;
            _burning = burning;
        }

        private void Start()
        {
            _engine = CreateLoop(engine);
            _siren = CreateLoop(siren);
            _skid = CreateLoop(skid);
            _hose = CreateLoop(hose);
            _steam = CreateLoop(steam);
            _fire = CreateLoop(fire);
            _ambience = CreateLoop(ambience);
            modes.VehicleEntered += OnVehicleEntered;
            modes.VehicleExited += OnVehicleExited;
            _onFootTime = modes.IsDriving ? 0f : walkMusicDelay;
            _music.PlayMusic(modes.IsDriving ? driveMusic : walkMusic, musicCrossfade);
        }

        private void OnDestroy()
        {
            modes.VehicleEntered -= OnVehicleEntered;
            modes.VehicleExited -= OnVehicleExited;
            Release(ref _engine);
            Release(ref _siren);
            Release(ref _skid);
            Release(ref _hose);
            Release(ref _steam);
            Release(ref _fire);
            Release(ref _ambience);
        }

        private void OnVehicleEntered(Vehicle vehicle)
        {
            _sfx.PlaySfx(doorClose);
        }

        private void OnVehicleExited(Vehicle vehicle)
        {
            // A runaway that stops later is not a door: only a calm exit right next to the car.
            if (!modes.IsDriving && Vector3.Distance(vehicle.transform.position, listener.position) < 8f)
            {
                _sfx.PlaySfx(doorOpen);
            }
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            UpdateCar(dt);
            UpdateSiren(dt);
            Drive(ref _hose, _nozzle.IsFlowing ? 1f : 0f, 1f, dt);
            Drive(ref _steam, _fires.IsDousing ? 1f : 0f, 1f, dt);
            Drive(ref _fire, FireLoudness(), 1f, dt);
            Drive(ref _ambience, 1f, 1f, dt);
            UpdateFootsteps(dt);
            UpdateMusic(dt);
        }

        private void UpdateCar(float dt)
        {
            var car = modes.MovingCar;
            if (car == null)
            {
                Drive(ref _engine, 0f, 1f, dt);
                Drive(ref _skid, 0f, 1f, dt);
                return;
            }

            // Fake gearbox: the revs climb through each gear and drop on the shift.
            var speed = Mathf.Abs(car.Controller.ForwardSpeed);
            var fraction = Mathf.Clamp01(speed / MaxCarSpeed);
            var gear = Mathf.Min(fraction * Gears, Gears - 0.001f);
            var revs = gear - Mathf.Floor(gear);
            var pitch = 0.85f + 0.35f * revs + 0.45f * fraction;
            if (car.IsAirborne)
            {
                pitch += 0.25f;
            }

            var heavy = car == modes.FireTruck;
            Drive(ref _engine, modes.IsDriving ? 1f : 0.6f, heavy ? pitch * 0.8f : pitch, dt);
            Drive(ref _skid, Slip(car), 1f, dt, skidResponse);
        }

        /// <summary>
        /// Only a real sideways slide squeals. Wheel slip values are high under plain throttle, so they would
        /// make the tyres scream all the time; the body's sideways speed is not.
        /// </summary>
        private static float Slip(Vehicle car)
        {
            if (car.IsAirborne || car.Speed < MinSkidSpeed)
            {
                return 0f;
            }

            var sideways = Mathf.Abs(Vector3.Dot(car.Body.linearVelocity, car.transform.right));
            return Mathf.Clamp01((sideways - SkidSidewaysSpeed) / 4f);
        }

        private void UpdateSiren(float dt)
        {
            var truck = modes.FireTruck;
            // The siren wails with the beacons (SirenLights): while the truck is driven.
            var on = truck != null && truck.Mode == VehicleMode.Driven && truck.Speed > SirenMinSpeed;
            var volume = 0f;
            if (on)
            {
                var distance = Vector3.Distance(truck.transform.position, listener.position);
                volume = Mathf.Clamp01(1f - distance / sirenHearingDistance);
            }

            Drive(ref _siren, volume, 1f, dt);
        }

        private float FireLoudness()
        {
            var loudness = 0f;
            var fires = _fires.Fires;
            var here = listener.position;
            for (var i = 0; i < fires.Count; i++)
            {
                var building = fires[i];
                if (!building.IsBurning)
                {
                    continue;
                }

                var distance = Vector3.Distance(building.Area.ClosestPoint(here), here);
                var near = Mathf.Clamp01(1f - distance / fireHearingDistance);
                loudness += near * near * Mathf.Lerp(0.4f, 1f, building.Strength);
            }

            return Mathf.Clamp01(Mathf.Max(loudness, _burning.Heat));
        }

        private void UpdateFootsteps(float dt)
        {
            if (modes.IsDriving)
            {
                _stride = 0f;
                _wasGrounded = true;
                return;
            }

            var grounded = walker.IsGrounded;
            if (!grounded)
            {
                _airVelocity = Mathf.Min(_airVelocity, walker.VerticalSpeed);
            }
            else if (!_wasGrounded)
            {
                if (_airVelocity < HardLandingSpeed)
                {
                    _sfx.PlaySfx(land, Mathf.Clamp01(-_airVelocity / 15f));
                }
                else
                {
                    footsteps.Play(_sfx);
                }

                _airVelocity = 0f;
                _stride = 0f;
            }

            _wasGrounded = grounded;
            var speed = walker.HorizontalSpeed;
            if (!grounded || speed < 0.8f)
            {
                return;
            }

            _stride += speed * dt;
            if (_stride >= StrideLength)
            {
                _stride -= StrideLength;
                footsteps.Play(_sfx, Mathf.Lerp(0.6f, 1f, speed / 9f));
            }
        }

        private void UpdateMusic(float dt)
        {
            _onFootTime = modes.IsDriving ? 0f : _onFootTime + dt;
            var current = _music.CurrentMusic;
            if (modes.IsDriving && current != driveMusic)
            {
                _music.CrossfadeMusic(driveMusic, musicCrossfade);
            }
            else if (_onFootTime > walkMusicDelay && current != walkMusic)
            {
                _music.CrossfadeMusic(walkMusic, musicCrossfade);
            }
        }

        private Loop CreateLoop(SoundData sound)
        {
            return new Loop { Handle = _loops.CreateLoopingSfxHandle(), Sound = sound };
        }

        private void Release(ref Loop loop)
        {
            _loops.ReleaseLoopingSfxHandle(loop.Handle, 0f);
        }

        /// <summary>Eases the loop toward <paramref name="target"/> (0..1 of its sound's volume); starts and stops it.</summary>
        private void Drive(ref Loop loop, float target, float pitch, float dt)
        {
            Drive(ref loop, target, pitch, dt, loopResponse);
        }

        private void Drive(ref Loop loop, float target, float pitch, float dt, float response)
        {
            loop.Volume = Mathf.MoveTowards(loop.Volume, target, response * dt);
            if (loop.Volume <= 0.001f)
            {
                if (loop.Playing)
                {
                    _loops.StopLoopingSfx(loop.Handle, 0f);
                    loop.Playing = false;
                }

                return;
            }

            if (!loop.Playing)
            {
                _loops.PlayLoopingSfx(loop.Handle, loop.Sound.Clip, 0f, pitch, 0f);
                loop.Playing = true;
            }

            _loops.SetLoopingSfxVolume(loop.Handle, loop.Volume * loop.Sound.Volume);
            _loops.SetLoopingSfxPitch(loop.Handle, pitch);
        }
    }
}
