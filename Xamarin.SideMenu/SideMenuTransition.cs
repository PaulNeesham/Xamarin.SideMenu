﻿using CoreGraphics;
using Foundation;
using System;
using System.Collections.Generic;
using System.Text;
using UIKit;

namespace Xamarin.SideMenu
{
    public class SideMenuTransition : UIPercentDrivenInteractiveTransition
    {
        public SideMenuManager SideMenuManager { get; set; }

        public SideMenuTransition(SideMenuManager sideMenuManager)
        {
            SideMenuManager = sideMenuManager;
        }

        protected override void Dispose(bool disposing)
        {
            if(disposing)
            {
                SideMenuManager = null;
            }

            base.Dispose(disposing);
        }

        private SideMenuAnimatedTransitioning animatedTransitioning;
        public SideMenuAnimatedTransitioning AnimatedTransitioning
        {
            get
            {
                if (animatedTransitioning == null)
                    animatedTransitioning = new SideMenuAnimatedTransitioning(this);

                return animatedTransitioning;
            }
        }

        private SideMenuTransitioningDelegate transitioningDelegate;
        public SideMenuTransitioningDelegate TransitioningDelegate
        {
            get
            {
                if (transitioningDelegate == null)
                    transitioningDelegate = new SideMenuTransitioningDelegate(this);

                return transitioningDelegate;
            }
        }

        public bool presenting = false;
        private bool interactive = false;
        private UIView originalSuperview;
        private bool switchMenus = false;

        public UIRectEdge presentDirection = UIRectEdge.Left;
        public UIView tapView;
        public UIView statusBarView;

        UIViewController viewControllerForPresentedMenu
        {
            get
            {
                return SideMenuManager.LeftNavigationController?.PresentingViewController != null
                    ? SideMenuManager.LeftNavigationController?.PresentingViewController
                    : SideMenuManager.RightNavigationController?.PresentingViewController;
            }
        }

        UIViewController visibleViewController
        {
            get
            {
                return getVisibleViewControllerFromViewController(UIApplication.SharedApplication.KeyWindow?.RootViewController);
            }
        }

        private UIViewController getVisibleViewControllerFromViewController(UIViewController viewController)
        {
            var navigationController = viewController as UINavigationController;
            if (navigationController != null)
                return getVisibleViewControllerFromViewController(navigationController.VisibleViewController);

            var tabBarController = viewController as UITabBarController;
            if (tabBarController != null)
                return getVisibleViewControllerFromViewController(tabBarController.SelectedViewController);

            var presentedViewController = viewController?.PresentedViewController;
            if (presentedViewController != null)
                return getVisibleViewControllerFromViewController(presentedViewController);

            return viewController;
        }

        public void handlePresentMenuLeftScreenEdge(UIScreenEdgePanGestureRecognizer edge)
        {
            this.presentDirection = UIRectEdge.Left;
            handlePresentMenuPan(edge);
        }

        public void handlePresentMenuRightScreenEdge(UIScreenEdgePanGestureRecognizer edge)
        {
            this.presentDirection = UIRectEdge.Right;
            handlePresentMenuPan(edge);
        }

        public void handlePresentMenuPan(UIPanGestureRecognizer pan)
        {
            // how much distance have we panned in reference to the parent view?
            var view = viewControllerForPresentedMenu != null ? viewControllerForPresentedMenu?.View : pan.View;
            if (view == null)
            {
                return;
            }

            var transform = view.Transform;
            view.Transform = CoreGraphics.CGAffineTransform.MakeIdentity();
            var translation = pan.TranslationInView(pan.View);
            view.Transform = transform;

            // do some math to translate this to a percentage based value
            if (!interactive) {
                if (translation.X == 0) {
                    return; // not sure which way the user is swiping yet, so do nothing
                }

                if (!(pan is UIScreenEdgePanGestureRecognizer)) {
                    this.presentDirection = translation.X > 0 ? UIRectEdge.Left : UIRectEdge.Right;
                }

                var menuViewController = this.presentDirection == UIRectEdge.Left
                    ? SideMenuManager.LeftNavigationController
                    : SideMenuManager.RightNavigationController;
                if (menuViewController != null && visibleViewController != null)
                {
                    interactive = true;
                    visibleViewController.PresentViewController(menuViewController, true, null);
                }
            }

            var direction = this.presentDirection == UIRectEdge.Left ? 1 : -1;
            var distance = translation.X / SideMenuManager.MenuWidth;
            // now lets deal with different states that the gesture recognizer sends
            switch (pan.State)
            {
                case UIGestureRecognizerState.Began:
                case UIGestureRecognizerState.Changed:
                    if (pan is UIScreenEdgePanGestureRecognizer) {
                        this.UpdateInteractiveTransition((float)Math.Min(distance * direction, 1));
                    }
                    else if (distance > 0 && this.presentDirection == UIRectEdge.Right && SideMenuManager.LeftNavigationController != null) {
                        this.presentDirection = UIRectEdge.Left;
                        switchMenus = true;
                        this.CancelInteractiveTransition();
                    }
                    else if (distance < 0 && this.presentDirection == UIRectEdge.Left && SideMenuManager.RightNavigationController != null) {
                        this.presentDirection = UIRectEdge.Right;
                        switchMenus = true;
                        this.CancelInteractiveTransition();
                    }
                    else
                    {
                        this.UpdateInteractiveTransition((float)Math.Min(distance * direction, 1));
                    }
                    break;

                default:
                    interactive = false;
                    view.Transform = CGAffineTransform.MakeIdentity();
                    var velocity = pan.VelocityInView(pan.View).X * direction;
                    view.Transform = transform;
                    if (velocity >= 100 || velocity >= -50 && Math.Abs(distance) >= 0.5)
                    {
                        //TODO: Review this... Uses FLT_EPSILON
                        //// bug workaround: animation briefly resets after call to finishInteractiveTransition() but before animateTransition completion is called.
                        //if (NSProcessInfo.ProcessInfo.OperatingSystemVersion.Major == 8 && this.percentComplete > 1f - 1.192092896e-07F) {
                        //            this.updateInteractiveTransition(0.9999);
                        //}
                        this.FinishInteractiveTransition();
                    }
                    else
                    {
                        this.CancelInteractiveTransition();
                    }
                    break;
            }
        }

        public void handleHideMenuPan(UIPanGestureRecognizer pan)
        {
            var translation = pan.TranslationInView(pan.View);
            var direction = this.presentDirection == UIRectEdge.Left ? -1 : 1;
            var distance = translation.X / SideMenuManager.MenuWidth * direction;
            
            switch (pan.State)
            {
                case UIGestureRecognizerState.Began:
                    interactive = true;
                    viewControllerForPresentedMenu?.DismissViewController(true, null);
                    break;
                case UIGestureRecognizerState.Changed:
                    this.UpdateInteractiveTransition((float)Math.Max(Math.Min(distance, 1), 0));
                    break;
                default:
                    interactive = false;
                    var velocity = pan.VelocityInView(pan.View).X * direction;
                    if (velocity >= 100 || velocity >= -50 && distance >= 0.5)
                    {
                        ////TODO: Review this... Uses FLT_EPSILON
                        //// bug workaround: animation briefly resets after call to finishInteractiveTransition() but before animateTransition completion is called.
                        //if (NSProcessInfo.ProcessInfo.OperatingSystemVersion.Major == 8 && this.PercentComplete > 1 - 1.192092896e-07F)
                        //{
                        //    this.UpdateInteractiveTransition(0.9999);
                        //}
                        this.FinishInteractiveTransition();
                    }
                    else
                    {
                        this.CancelInteractiveTransition();
                    }
                    break;
            }
        }

        void handleHideMenuTap(UITapGestureRecognizer tap)
        {
            viewControllerForPresentedMenu?.DismissViewController(true, null);
        }

        public void hideMenuStart()
        {
            if(menuObserver != null)
                NSNotificationCenter.DefaultCenter.RemoveObserver(menuObserver);

            var mainViewController = this.viewControllerForPresentedMenu;
            var menuView = this.presentDirection == UIRectEdge.Left ? SideMenuManager.LeftNavigationController?.View : SideMenuManager.RightNavigationController?.View;
            if (mainViewController == null || menuView == null)
                return;

            menuView.Transform = CGAffineTransform.MakeIdentity();
            mainViewController.View.Transform = CGAffineTransform.MakeIdentity();
            mainViewController.View.Alpha = 1;
            this.tapView.Frame = new CGRect(0, 0, mainViewController.View.Frame.Width, mainViewController.View.Frame.Height);
            var frame = menuView.Frame;
            frame.Y = 0;
            frame.Size = new CGSize(SideMenuManager.MenuWidth, mainViewController.View.Frame.Height);
            menuView.Frame = frame;
            if (this.statusBarView != null)
            {
                this.statusBarView.Frame = UIApplication.SharedApplication.StatusBarFrame;
                this.statusBarView.Alpha = 0;
            }

            CGRect menuFrame;
            CGRect viewFrame;
            switch (SideMenuManager.PresentMode)
            {
                case SideMenuManager.MenuPresentMode.ViewSlideOut:
                    menuView.Alpha = 1 - (float)SideMenuManager.AnimationFadeStrength;

                    menuFrame = menuView.Frame;
                    menuFrame.X = (float)(this.presentDirection == UIRectEdge.Left ? 0 : mainViewController.View.Frame.Width - SideMenuManager.MenuWidth);
                    menuView.Frame = menuFrame;

                    viewFrame = mainViewController.View.Frame;
                    viewFrame.X = 0;
                    mainViewController.View.Frame = viewFrame;

                    menuView.Transform = CGAffineTransform.MakeScale((float)SideMenuManager.AnimationTransformScaleFactor, (float)SideMenuManager.AnimationTransformScaleFactor);
                    break;

                case SideMenuManager.MenuPresentMode.ViewSlideInOut:
                    menuView.Alpha = 1;

                    menuFrame = menuView.Frame;
                    menuFrame.X = this.presentDirection == UIRectEdge.Left ? -menuView.Frame.Width : mainViewController.View.Frame.Width;
                    menuView.Frame = menuFrame;

                    viewFrame = mainViewController.View.Frame;
                    viewFrame.X = 0;
                    mainViewController.View.Frame = viewFrame;
                    break;

                case SideMenuManager.MenuPresentMode.MenuSlideIn:
                    menuView.Alpha = 1;

                    menuFrame = menuView.Frame;
                    menuFrame.X = this.presentDirection == UIRectEdge.Left ? -menuView.Frame.Width : mainViewController.View.Frame.Width;
                    menuView.Frame = menuFrame;
                    break;

                case SideMenuManager.MenuPresentMode.MenuDissolveIn:
                    menuView.Alpha = 0;

                    menuFrame = menuView.Frame;
                    menuFrame.X = (float)(this.presentDirection == UIRectEdge.Left ? 0 : mainViewController.View.Frame.Width - SideMenuManager.MenuWidth);
                    menuView.Frame = menuFrame;

                    viewFrame = mainViewController.View.Frame;
                    viewFrame.X = 0;
                    mainViewController.View.Frame = viewFrame;
                    break;
            }
        }

        public void hideMenuComplete()
        {
            var mainViewController = this.viewControllerForPresentedMenu;
            var menuView = this.presentDirection == UIRectEdge.Left ? SideMenuManager.LeftNavigationController?.View : SideMenuManager.RightNavigationController?.View;
            if (mainViewController == null || menuView == null)
            {
                return;
            }

            this.tapView.RemoveFromSuperview();
            this.statusBarView?.RemoveFromSuperview();
            mainViewController.View.MotionEffects = new List<UIMotionEffect>().ToArray();
            mainViewController.View.Layer.ShadowOpacity = 0;
            menuView.Layer.ShadowOpacity = 0;
            var topNavigationController = mainViewController as UINavigationController;
            if (topNavigationController != null)
            {
                topNavigationController.InteractivePopGestureRecognizer.Enabled = true;
            }

            originalSuperview?.AddSubview(mainViewController.View);
        }

        public void presentMenuStart(CGSize? size = null)
        {
            if (size == null)
                size = SideMenuManager.appScreenRect.Size;

            var menuView = this.presentDirection == UIRectEdge.Left ? SideMenuManager.LeftNavigationController?.View : SideMenuManager.RightNavigationController?.View;
            var mainViewController = this.viewControllerForPresentedMenu;
            if (menuView == null || mainViewController == null)
                return;

            menuView.Transform = CGAffineTransform.MakeIdentity();
            mainViewController.View.Transform = CGAffineTransform.MakeIdentity();
            var menuFrame = menuView.Frame;
            menuFrame.Size = new CGSize(SideMenuManager.MenuWidth, size.Value.Height);
            menuFrame.X = (float)(this.presentDirection == UIRectEdge.Left ? 0 : size.Value.Width - SideMenuManager.MenuWidth);
            menuView.Frame = menuFrame;

            if (this.statusBarView != null)
            {
                this.statusBarView.Frame = UIApplication.SharedApplication.StatusBarFrame;
                this.statusBarView.Alpha = 1;
            }

            int direction = 0;
            CGRect frame;
            switch (SideMenuManager.PresentMode)
            {
                case SideMenuManager.MenuPresentMode.ViewSlideOut:
                    menuView.Alpha = 1;
                    direction = this.presentDirection == UIRectEdge.Left ? 1 : -1;
                    frame = mainViewController.View.Frame;
                    frame.X = direction * (menuView.Frame.Width);
                    mainViewController.View.Frame = frame;
                    mainViewController.View.Layer.ShadowColor = SideMenuManager.ShadowColor.CGColor;
                    mainViewController.View.Layer.ShadowRadius = (float)SideMenuManager.ShadowRadius;
                    mainViewController.View.Layer.ShadowOpacity = (float)SideMenuManager.ShadowOpacity;
                    mainViewController.View.Layer.ShadowOffset = new CGSize(0, 0);
                    break;

                case SideMenuManager.MenuPresentMode.ViewSlideInOut:
                    menuView.Alpha = 1;
                    menuView.Layer.ShadowColor = SideMenuManager.ShadowColor.CGColor;
                    menuView.Layer.ShadowRadius = (float)SideMenuManager.ShadowRadius;
                    menuView.Layer.ShadowOpacity = (float)SideMenuManager.ShadowOpacity;
                    menuView.Layer.ShadowOffset = new CGSize(0, 0);
                    direction = this.presentDirection == UIRectEdge.Left ? 1 : -1;
                    frame = mainViewController.View.Frame;
                    frame.X = direction * (menuView.Frame.Width);
                    mainViewController.View.Frame = frame;
                    mainViewController.View.Transform = CGAffineTransform.MakeScale((float)SideMenuManager.AnimationTransformScaleFactor, (float)SideMenuManager.AnimationTransformScaleFactor);
                    mainViewController.View.Alpha = (float)(1 - SideMenuManager.AnimationFadeStrength);
                    break;

                case SideMenuManager.MenuPresentMode.MenuSlideIn:
                case SideMenuManager.MenuPresentMode.MenuDissolveIn:
                    menuView.Alpha = 1;
                    menuView.Layer.ShadowColor = SideMenuManager.ShadowColor.CGColor;
                    menuView.Layer.ShadowRadius = (float)SideMenuManager.ShadowRadius;
                    menuView.Layer.ShadowOpacity = (float)SideMenuManager.ShadowOpacity;
                    menuView.Layer.ShadowOffset = new CGSize(0, 0);
                    mainViewController.View.Frame = new CGRect(0, 0, size.Value.Width, size.Value.Height);
                    mainViewController.View.Transform = CGAffineTransform.MakeScale((float)SideMenuManager.AnimationTransformScaleFactor, (float)SideMenuManager.AnimationTransformScaleFactor);
                    mainViewController.View.Alpha = (float)(1 - SideMenuManager.AnimationFadeStrength);
                    break;
            }
        }

        NSObject menuObserver;
        void presentMenuComplete()
        {
            //TODO: Review this
            menuObserver = NSNotificationCenter.DefaultCenter.AddObserver(UIApplication.DidEnterBackgroundNotification, (_) => TransitioningDelegate.applicationDidEnterBackgroundNotification());

            var mainViewController = this.viewControllerForPresentedMenu;
            if (mainViewController == null)
                return;

            switch (SideMenuManager.PresentMode) {
                case SideMenuManager.MenuPresentMode.MenuSlideIn:
                case SideMenuManager.MenuPresentMode.MenuDissolveIn:
                case SideMenuManager.MenuPresentMode.ViewSlideInOut:
                    if (SideMenuManager.ParallaxStrength != 0) {
                        var horizontal = new UIInterpolatingMotionEffect(keyPath: "center.x", type: UIInterpolatingMotionEffectType.TiltAlongHorizontalAxis);
                        horizontal.MinimumRelativeValue = NSNumber.FromInt32(-SideMenuManager.ParallaxStrength);
                        horizontal.MinimumRelativeValue = NSNumber.FromInt32(SideMenuManager.ParallaxStrength);

                        var vertical = new UIInterpolatingMotionEffect(keyPath: "center.y", type: UIInterpolatingMotionEffectType.TiltAlongVerticalAxis);
                        vertical.MinimumRelativeValue = NSNumber.FromInt32(- SideMenuManager.ParallaxStrength);
                        vertical.MaximumRelativeValue = NSNumber.FromInt32(SideMenuManager.ParallaxStrength);

                        var group = new UIMotionEffectGroup();
                        group.MotionEffects = new UIMotionEffect[] { horizontal, vertical };
                        mainViewController.View.AddMotionEffect(group);
                    }
                    break;
                case SideMenuManager.MenuPresentMode.ViewSlideOut:
                    break;
            }

            var topNavigationController = mainViewController as UINavigationController;
            if (topNavigationController != null) {
                topNavigationController.InteractivePopGestureRecognizer.Enabled = false;
            }
        }

        // MARK: UIViewControllerAnimatedTransitioning protocol methods

        public class SideMenuAnimatedTransitioning : UIViewControllerAnimatedTransitioning
        {
            private SideMenuTransition _sideMenuTransition;
            public SideMenuAnimatedTransitioning(SideMenuTransition sideMenuTransition)
            {
                _sideMenuTransition = sideMenuTransition;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _sideMenuTransition = null;
                }

                base.Dispose(disposing);
            }

            // animate a change from one viewcontroller to another
            public override void AnimateTransition(IUIViewControllerContextTransitioning transitionContext)
            {
                // get reference to our fromView, toView and the container view that we should perform the transition in
                var container = transitionContext.ContainerView;
                var menuBackgroundColor = _sideMenuTransition.SideMenuManager.AnimationBackgroundColor;
                if (menuBackgroundColor != null)
                {
                    container.BackgroundColor = menuBackgroundColor;
                }

                // create a tuple of our screens
                var screens = new
                {
                    from = transitionContext.GetViewControllerForKey(UITransitionContext.FromViewControllerKey),
                    to = transitionContext.GetViewControllerForKey(UITransitionContext.ToViewControllerKey)
                };

                // assign references to our menu view controller and the 'bottom' view controller from the tuple
                // remember that our menuViewController will alternate between the from and to view controller depending if we're presenting or dismissing
                var menuViewController = (!_sideMenuTransition.presenting ? screens.from : screens.to);
                var topViewController = !_sideMenuTransition.presenting ? screens.to : screens.from;

                var menuView = menuViewController.View;
                var topView = topViewController.View;

                // prepare menu items to slide in
                if (_sideMenuTransition.presenting)
                {
                    var tapView = new UIView();
                    tapView.AutoresizingMask = UIViewAutoresizing.FlexibleHeight | UIViewAutoresizing.FlexibleWidth;
                    var exitPanGesture = new UIPanGestureRecognizer();
                    exitPanGesture.AddTarget(/*SideMenuTransition.Current, */() => _sideMenuTransition.handleHideMenuPan(exitPanGesture));
                    var exitTapGesture = new UITapGestureRecognizer();
                    exitTapGesture.AddTarget(/*SideMenuTransition.Current, */() => _sideMenuTransition.handleHideMenuTap(exitTapGesture));
                    tapView.AddGestureRecognizer(exitPanGesture);
                    tapView.AddGestureRecognizer(exitTapGesture);
                    _sideMenuTransition.tapView = tapView;

                    _sideMenuTransition.originalSuperview = topView.Superview;

                    // add the both views to our view controller
                    switch (_sideMenuTransition.SideMenuManager.PresentMode)
                    {
                        case SideMenuManager.MenuPresentMode.ViewSlideOut:
                            container.AddSubview(menuView);
                            container.AddSubview(topView);
                            topView.AddSubview(tapView);
                            break;
                        case SideMenuManager.MenuPresentMode.MenuSlideIn:
                        case SideMenuManager.MenuPresentMode.MenuDissolveIn:
                        case SideMenuManager.MenuPresentMode.ViewSlideInOut:
                            container.AddSubview(topView);
                            container.AddSubview(tapView);
                            container.AddSubview(menuView);
                            break;
                    }

                    if (_sideMenuTransition.SideMenuManager.FadeStatusBar)
                    {
                        var blackBar = new UIView();
                        var menuShrinkBackgroundColor = _sideMenuTransition.SideMenuManager.AnimationBackgroundColor;
                        if (menuShrinkBackgroundColor != null)
                        {
                            blackBar.BackgroundColor = menuShrinkBackgroundColor;
                        }
                        else
                        {
                            blackBar.BackgroundColor = UIColor.Black;
                        }
                        blackBar.UserInteractionEnabled = false;
                        container.AddSubview(blackBar);
                        _sideMenuTransition.statusBarView = blackBar;
                    }

                    _sideMenuTransition.hideMenuStart(); // offstage for interactive
                }

                // perform the animation!
                var duration = TransitionDuration(transitionContext);
                var options = _sideMenuTransition.interactive ? UIViewAnimationOptions.CurveLinear : UIViewAnimationOptions.CurveEaseInOut;
                UIView.Animate(duration, 0, options,
                    animation: () =>
                    {
                        if (_sideMenuTransition.presenting)
                        {
                            _sideMenuTransition.presentMenuStart(); // onstage items: slide in
                    }
                        else
                        {
                            _sideMenuTransition.hideMenuStart();
                        }
                        menuView.UserInteractionEnabled = false;
                    },
                    completion: () =>
                    {
                    // tell our transitionContext object that we've finished animating
                    if (transitionContext.TransitionWasCancelled)
                        {
                            var viewControllerForPresentedMenu = _sideMenuTransition.viewControllerForPresentedMenu;

                            if (_sideMenuTransition.presenting)
                            {
                                _sideMenuTransition.hideMenuComplete();
                            }
                            else
                            {
                                _sideMenuTransition.presentMenuComplete();
                            }
                            menuView.UserInteractionEnabled = true;

                            transitionContext.CompleteTransition(false);


                            if (_sideMenuTransition.switchMenus)
                            {
                                _sideMenuTransition.switchMenus = false;
                                viewControllerForPresentedMenu?.PresentViewController(
                                    _sideMenuTransition.presentDirection == UIRectEdge.Left
                                        ? _sideMenuTransition.SideMenuManager.LeftNavigationController
                                        : _sideMenuTransition.SideMenuManager.RightNavigationController,
                                    true, null);
                            }

                            return;
                        }

                        if (_sideMenuTransition.presenting)
                        {
                            _sideMenuTransition.presentMenuComplete();
                            menuView.UserInteractionEnabled = true;
                            transitionContext.CompleteTransition(true);
                            switch (_sideMenuTransition.SideMenuManager.PresentMode)
                            {
                                case SideMenuManager.MenuPresentMode.ViewSlideOut:
                                    container.AddSubview(topView);
                                    break;
                                case SideMenuManager.MenuPresentMode.MenuSlideIn:
                                case SideMenuManager.MenuPresentMode.MenuDissolveIn:
                                case SideMenuManager.MenuPresentMode.ViewSlideInOut:
                                    container.InsertSubview(topView, atIndex: 0);
                                    break;
                            }

                            var statusBarView = _sideMenuTransition.statusBarView;
                            if (statusBarView != null)
                            {
                                container.BringSubviewToFront(statusBarView);
                            }
                            return;
                        }

                        _sideMenuTransition.hideMenuComplete();
                        transitionContext.CompleteTransition(true);
                        menuView.RemoveFromSuperview();
                    });
            }

            // return how many seconds the transiton animation will take
            public override double TransitionDuration(IUIViewControllerContextTransitioning transitionContext)
            {
                return _sideMenuTransition.presenting ? _sideMenuTransition.SideMenuManager.AnimationPresentDuration : _sideMenuTransition.SideMenuManager.AnimationDismissDuration;
            }
        }


        // MARK: UIViewControllerTransitioningDelegate protocol methods

        public class SideMenuTransitioningDelegate : UIViewControllerTransitioningDelegate
        {
            private SideMenuTransition _sideMenuTransition;
            public SideMenuTransitioningDelegate(SideMenuTransition sideMenuTransition)
            {
                _sideMenuTransition = sideMenuTransition;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _sideMenuTransition = null;
                }

                base.Dispose(disposing);
            }

            // return the animator when presenting a viewcontroller
            // rememeber that an animator (or animation controller) is any object that aheres to the UIViewControllerAnimatedTransitioning protocol
            public override IUIViewControllerAnimatedTransitioning GetAnimationControllerForPresentedController(UIViewController presented, UIViewController presentingViewController, UIViewController source)
            {
                _sideMenuTransition.presenting = true;
                _sideMenuTransition.presentDirection = presented == _sideMenuTransition.SideMenuManager.LeftNavigationController ? UIRectEdge.Left : UIRectEdge.Right;
                return _sideMenuTransition.AnimatedTransitioning;
            }

            public override IUIViewControllerAnimatedTransitioning GetAnimationControllerForDismissedController(UIViewController dismissed)
            {
                _sideMenuTransition.presenting = false;
                return _sideMenuTransition.AnimatedTransitioning;
            }

            public override IUIViewControllerInteractiveTransitioning GetInteractionControllerForPresentation(IUIViewControllerAnimatedTransitioning animator)
            {
                // if our interactive flag is true, return the transition manager object
                // otherwise return nil
                //TODO: Fix this. Cast not working...
                return null;// interactive ? SideMenuTransition.Current : null;
            }

            public override IUIViewControllerInteractiveTransitioning GetInteractionControllerForDismissal(IUIViewControllerAnimatedTransitioning animator)
            {
                //TODO: Fix this. Cast not working...
                return null;// interactive ? SideMenuTransition.Current : null;
            }

            public void applicationDidEnterBackgroundNotification()
            {
                var menuViewController = _sideMenuTransition.presentDirection == UIRectEdge.Left ? _sideMenuTransition.SideMenuManager.LeftNavigationController : _sideMenuTransition.SideMenuManager.RightNavigationController;
                if (menuViewController != null)
                {
                    _sideMenuTransition.hideMenuStart();
                    _sideMenuTransition.hideMenuComplete();
                    menuViewController.DismissViewController(false, null);
                }
            }
        }
    }
}