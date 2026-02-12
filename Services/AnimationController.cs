using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Media;

namespace VALORANT_Overlay.Services
{
    //add new animation types here, then in LoadAnimations, then you can call it with animationController.Play(AnimationType.xxx)
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
        private readonly Image _displayImage;
        private readonly DispatcherTimer _timer = new();
        private readonly Dictionary<AnimationType, AnimationDefinition> _animations = new();

        private AnimationDefinition _currentAnimation;
        private int _currentFrameIndex;
        private AnimationType _currentIdleType = AnimationType.IdleMain;

        public AnimationController(Image displayImage)
        {
            _displayImage = displayImage;
            _timer.Tick += OnFrameTick;
        }

        //Load frames from Assets/Animations/<type> folder
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

                //Frame duration, priority<-(unused as either 0 or 100)
                anim.FrameDuration = type switch
                {
                    AnimationType.IdleMain => TimeSpan.FromMilliseconds(700),
                    AnimationType.IdlePistol => TimeSpan.FromMilliseconds(700),
                    AnimationType.IdleKnife => TimeSpan.FromMilliseconds(700),
                    AnimationType.WeaponSwap => TimeSpan.FromMilliseconds(100),
                    AnimationType.Kill => TimeSpan.FromMilliseconds(150),
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

            //main
            Play(_currentIdleType);
        }

        public void SetIdleType(AnimationType idleType)
        {
            _currentIdleType = idleType;

            //immediately switch to Idle if no high-priority animation (kill, weaponswap) is active
            if (_currentAnimation == null || _currentAnimation.Priority == 0)
            {
                Play(_currentIdleType);
            }
        }

        public void Play(AnimationType type)
        {
            if (!_animations.ContainsKey(type)) return;

            if (type == AnimationType.IdleKnife)
            {
                _displayImage.RenderTransform = new TranslateTransform(40, 175); // Positive Y = Down
            }
            else
            {
                _displayImage.RenderTransform = new TranslateTransform(0, 0);
            }

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

        public System.Windows.Size GetIdleSize()
        {
            if (_animations.ContainsKey(AnimationType.IdleMain) && _animations[AnimationType.IdleMain].Frames.Count > 0)
            {
                var frame = _animations[AnimationType.IdleMain].Frames[0];
                return new System.Windows.Size(frame.PixelWidth, frame.PixelHeight);
            }
            return System.Windows.Size.Empty;
        }
    }
}
