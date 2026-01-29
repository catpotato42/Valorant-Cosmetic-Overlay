using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Controls;

namespace VALORANT_Overlay.Services
{
    public enum AnimationType
    {
        IdleMain,
        IdlePistol,
        IdleKnife,
        Kill,
        WeaponSwap
    }

    public class AnimationDefinition
    {
        public AnimationType Type { get; set; }
        public List<BitmapImage> Frames { get; set; } = new();
        public TimeSpan FrameDuration { get; set; } = TimeSpan.FromMilliseconds(100);
        public bool Loop { get; set; } = true;
        public int Priority { get; set; } = 0;
    }

    public class AnimationController
    {
        private const float ANIMATION_SCALE = 0.3f;
        private readonly Image _displayImage;
        private readonly DispatcherTimer _timer = new();
        private readonly Dictionary<AnimationType, AnimationDefinition> _animations = new();

        private AnimationDefinition _currentAnimation;
        private int _currentFrameIndex;
        private AnimationType _currentIdleType = AnimationType.IdleMain; //irrelevant to set as instantly sensed on program start
        private string _currentWeaponType; // "main", "pistol", "knife"

        public AnimationController(Image displayImage)
        {
            _displayImage = displayImage;
            _timer.Tick += OnFrameTick;
            
            // Apply scale transform to the image
            var scaleTransform = new System.Windows.Media.ScaleTransform(ANIMATION_SCALE, ANIMATION_SCALE);
            _displayImage.RenderTransform = scaleTransform;
        }

        // Load frames from Assets/Animations/<type> folder
        public void LoadAnimations()
        {
            foreach (AnimationType type in Enum.GetValues(typeof(AnimationType)))
            {
                string folder = Path.Combine("Assets", "Animations", type.ToString());
                if (!Directory.Exists(folder)) continue;

                var anim = new AnimationDefinition { Type = type };

                foreach (var file in Directory.GetFiles(folder, "*.png"))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(Path.GetFullPath(file));
                    bmp.EndInit();
                    
                    anim.Frames.Add(bmp);
                }

                // Assign default frame duration and priority
                anim.FrameDuration = type switch
                {
                    AnimationType.IdleMain => TimeSpan.FromMilliseconds(400),
                    AnimationType.IdlePistol => TimeSpan.FromMilliseconds(400),
                    AnimationType.IdleKnife => TimeSpan.FromMilliseconds(400),
                    AnimationType.WeaponSwap => TimeSpan.FromMilliseconds(100),
                    AnimationType.Kill => TimeSpan.FromMilliseconds(100),
                    _ => TimeSpan.FromMilliseconds(100)
                };
                anim.Priority = type switch
                {
                    AnimationType.IdleMain => 0,
                    AnimationType.IdlePistol => 0,
                    AnimationType.IdleKnife => 0,
                    AnimationType.WeaponSwap => 100,
                    AnimationType.Kill => 100,
                    _ => 0
                };

                if (type == AnimationType.IdleMain || type == AnimationType.IdlePistol || type == AnimationType.IdleKnife)
                    anim.Loop = true;
                else
                    anim.Loop = false;

                _animations[type] = anim;
            }

            // Start with Idle
            Play(_currentIdleType);
        }

        public void SetIdleType(AnimationType idleType)
        {
            _currentIdleType = idleType;

            // Immediately switch Idle if no high-priority animation is active
            if (_currentAnimation == null || _currentAnimation.Priority == 0)
            {
                Play(_currentIdleType);
            }
        }

        public void Play(AnimationType type)
        {
            if (!_animations.ContainsKey(type)) return;

            var next = _animations[type];
            //idle's priority is lowest, so we need a check to always play it after other animations finish
            if (type == _currentIdleType || _currentAnimation == null || next.Priority >= _currentAnimation.Priority)
            {
                _currentAnimation = next;
                _currentFrameIndex = 0;
                _timer.Interval = _currentAnimation.FrameDuration;
                _timer.Start();
                UpdateFrame();
            }
        }

        private void OnFrameTick(object sender, EventArgs e)
        {
            if (_currentAnimation == null || _currentAnimation.Frames.Count == 0) return;

            _currentFrameIndex++;

            if (_currentFrameIndex >= _currentAnimation.Frames.Count)
            {
                if (_currentAnimation.Loop)
                    _currentFrameIndex = 0;
                else
                {
                    // Return to Idle if non-looping animation finished
                    Play(_currentIdleType);
                    return;
                }
            }

            UpdateFrame();
        }

        private void UpdateFrame()
        {
            _displayImage.Source = _currentAnimation.Frames[_currentFrameIndex];
        }
    }
}
