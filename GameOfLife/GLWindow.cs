﻿using System;
using System.Diagnostics;
using System.Threading;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Platform;

namespace GameOfLife
{
    public class FrameArgs
    {
        public FrameArgs()
        {
        }

        public FrameArgs(double elapsed)
        {
            Time = Time;
        }

        public double Time { get; set; }
    }

    class GLWindow : NativeWindow, IGameWindow, IDisposable
    {
        private const double MaxFrequency = 500.0; // Frequency cap for Update/RenderFrame events

        private readonly Stopwatch watchRender = new Stopwatch();
        private readonly Stopwatch watchUpdate = new Stopwatch();
        private Thread updateThread;

        private IGraphicsContext glContext;

        private bool isExiting = false;

        private double update_period, render_period;
        private double target_update_period, target_render_period;

        private double update_time; // length of last UpdateFrame event
        private double render_time; // length of last RenderFrame event

        private double update_timestamp; // timestamp of last UpdateFrame event
        private double render_timestamp; // timestamp of last RenderFrame event
        private double render_last_overwait; // timestamp of last RenderFrame event

        private FrameArgs update_args = new FrameArgs();
        private FrameArgs render_args = new FrameArgs();

        /// <summary>Constructs a new GameWindow with sensible default attributes.</summary>
        public GLWindow()
            : this(640, 480, GraphicsMode.Default, "OpenTK Game Window", 0, DisplayDevice.Default) { }

        /// <summary>Constructs a new GameWindow with the specified attributes.</summary>
        /// <param name="width">The width of the GameWindow in pixels.</param>
        /// <param name="height">The height of the GameWindow in pixels.</param>
        public GLWindow(int width, int height)
            : this(width, height, GraphicsMode.Default, "OpenTK Game Window", 0, DisplayDevice.Default) { }

        /// <summary>Constructs a new GameWindow with the specified attributes.</summary>
        /// <param name="width">The width of the GameWindow in pixels.</param>
        /// <param name="height">The height of the GameWindow in pixels.</param>
        /// <param name="mode">The OpenTK.Graphics.GraphicsMode of the GameWindow.</param>
        public GLWindow(int width, int height, GraphicsMode mode)
            : this(width, height, mode, "OpenTK Game Window", 0, DisplayDevice.Default) { }

        /// <summary>Constructs a new GameWindow with the specified attributes.</summary>
        /// <param name="width">The width of the GameWindow in pixels.</param>
        /// <param name="height">The height of the GameWindow in pixels.</param>
        /// <param name="mode">The OpenTK.Graphics.GraphicsMode of the GameWindow.</param>
        /// <param name="title">The title of the GameWindow.</param>
        public GLWindow(int width, int height, GraphicsMode mode, string title)
            : this(width, height, mode, title, 0, DisplayDevice.Default) { }

        /// <summary>Constructs a new GameWindow with the specified attributes.</summary>
        /// <param name="width">The width of the GameWindow in pixels.</param>
        /// <param name="height">The height of the GameWindow in pixels.</param>
        /// <param name="mode">The OpenTK.Graphics.GraphicsMode of the GameWindow.</param>
        /// <param name="title">The title of the GameWindow.</param>
        /// <param name="options">GameWindow options regarding window appearance and behavior.</param>
        public GLWindow(int width, int height, GraphicsMode mode, string title, GameWindowFlags options)
            : this(width, height, mode, title, options, DisplayDevice.Default) { }

        /// <summary>Constructs a new GameWindow with the specified attributes.</summary>
        /// <param name="width">The width of the GameWindow in pixels.</param>
        /// <param name="height">The height of the GameWindow in pixels.</param>
        /// <param name="mode">The OpenTK.Graphics.GraphicsMode of the GameWindow.</param>
        /// <param name="title">The title of the GameWindow.</param>
        /// <param name="options">GameWindow options regarding window appearance and behavior.</param>
        /// <param name="device">The OpenTK.Graphics.DisplayDevice to construct the GameWindow in.</param>
        public GLWindow(int width, int height, GraphicsMode mode, string title, GameWindowFlags options, DisplayDevice device)
            : this(width, height, mode, title, options, device, 1, 0, GraphicsContextFlags.Default)
        { }

        /// <summary>Constructs a new GameWindow with the specified attributes.</summary>
        /// <param name="width">The width of the GameWindow in pixels.</param>
        /// <param name="height">The height of the GameWindow in pixels.</param>
        /// <param name="mode">The OpenTK.Graphics.GraphicsMode of the GameWindow.</param>
        /// <param name="title">The title of the GameWindow.</param>
        /// <param name="options">GameWindow options regarding window appearance and behavior.</param>
        /// <param name="device">The OpenTK.Graphics.DisplayDevice to construct the GameWindow in.</param>
        /// <param name="major">The major version for the OpenGL GraphicsContext.</param>
        /// <param name="minor">The minor version for the OpenGL GraphicsContext.</param>
        /// <param name="flags">The GraphicsContextFlags version for the OpenGL GraphicsContext.</param>
        public GLWindow(int width, int height, GraphicsMode mode, string title, GameWindowFlags options, DisplayDevice device,
            int major, int minor, GraphicsContextFlags flags)
            : this(width, height, mode, title, options, device, major, minor, flags, null)
        { }

        /// <summary>Constructs a new GameWindow with the specified attributes.</summary>
        /// <param name="width">The width of the GameWindow in pixels.</param>
        /// <param name="height">The height of the GameWindow in pixels.</param>
        /// <param name="mode">The OpenTK.Graphics.GraphicsMode of the GameWindow.</param>
        /// <param name="title">The title of the GameWindow.</param>
        /// <param name="options">GameWindow options regarding window appearance and behavior.</param>
        /// <param name="device">The OpenTK.Graphics.DisplayDevice to construct the GameWindow in.</param>
        /// <param name="major">The major version for the OpenGL GraphicsContext.</param>
        /// <param name="minor">The minor version for the OpenGL GraphicsContext.</param>
        /// <param name="flags">The GraphicsContextFlags version for the OpenGL GraphicsContext.</param>
        /// <param name="sharedContext">An IGraphicsContext to share resources with.</param>

        /// <summary>Constructs a new GameWindow with the specified attributes.</summary>
        /// <param name="width">The width of the GameWindow in pixels.</param>
        /// <param name="height">The height of the GameWindow in pixels.</param>
        /// <param name="mode">The OpenTK.Graphics.GraphicsMode of the GameWindow.</param>
        /// <param name="title">The title of the GameWindow.</param>
        /// <param name="options">GameWindow options regarding window appearance and behavior.</param>
        /// <param name="device">The OpenTK.Graphics.DisplayDevice to construct the GameWindow in.</param>
        /// <param name="major">The major version for the OpenGL GraphicsContext.</param>
        /// <param name="minor">The minor version for the OpenGL GraphicsContext.</param>
        /// <param name="flags">The GraphicsContextFlags version for the OpenGL GraphicsContext.</param>
        /// <param name="sharedContext">An IGraphicsContext to share resources with.</param>
        public GLWindow(int width, int height, GraphicsMode mode, string title, GameWindowFlags options, DisplayDevice device,
                          int major, int minor, GraphicsContextFlags flags, IGraphicsContext sharedContext)
            : base(width, height, title, options,
                   mode == null ? GraphicsMode.Default : mode,
                   device == null ? DisplayDevice.Default : device)
        {
            try
            {
                glContext = new GraphicsContext(mode == null ? GraphicsMode.Default : mode, WindowInfo, major, minor, flags);
                glContext.MakeCurrent(WindowInfo);
                (glContext as IGraphicsContextInternal).LoadAll();

                VSync = VSyncMode.On;

                //glWindow.WindowInfoChanged += delegate(object sender, EventArgs e) { OnWindowInfoChangedInternal(e); };
            }
            catch (Exception e)
            {
                Debug.Print(e.ToString());
                base.Dispose();
                throw;
            }
        }

        event EventHandler<FrameEventArgs> IGameWindow.UpdateFrame
        {
            add
            {
                throw new NotImplementedException();
            }

            remove
            {
                throw new NotImplementedException();
            }
        }

        event EventHandler<FrameEventArgs> IGameWindow.RenderFrame
        {
            add
            {
                throw new NotImplementedException();
            }

            remove
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Disposes of the GameWindow, releasing all resources consumed by it.
        /// </summary>
        public override void Dispose()
        {
            try
            {
                Dispose(true);
            }
            finally
            {
                try
                {
                    if (glContext != null)
                    {
                        glContext.Dispose();
                        glContext = null;
                    }
                }
                finally
                {
                    base.Dispose();
                }
            }
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Closes the GameWindow. Equivalent to <see cref="NativeWindow.Close"/> method.
        /// </summary>
        /// <remarks>
        /// <para>Override if you are not using <see cref="GameWindow.Run()"/>.</para>
        /// <para>If you override this method, place a call to base.Exit(), to ensure proper OpenTK shutdown.</para>
        /// </remarks>
        public virtual void Exit()
        {
            Close();
        }

        /// <summary>
        /// Makes the GraphicsContext current on the calling thread.
        /// </summary>
        public void MakeCurrent()
        {
            EnsureUndisposed();
            Context.MakeCurrent(WindowInfo);
        }

        /// <summary>
        /// Called when the NativeWindow is about to close.
        /// </summary>
        /// <param name="e">
        /// The <see cref="System.ComponentModel.CancelEventArgs" /> for this event.
        /// Set e.Cancel to true in order to stop the GameWindow from closing.</param>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            if (!e.Cancel)
            {
                isExiting = true;
                OnUnloadInternal(EventArgs.Empty);
            }
        }


        /// <summary>
        /// Called after an OpenGL context has been established, but before entering the main loop.
        /// </summary>
        /// <param name="e">Not used.</param>
        protected virtual void OnLoad(EventArgs e)
        {
            Load(this, e);
        }

        /// <summary>
        /// Called after GameWindow.Exit was called, but before destroying the OpenGL context.
        /// </summary>
        /// <param name="e">Not used.</param>
        protected virtual void OnUnload(EventArgs e)
        {
            Unload(this, e);
        }

        /// <summary>
        /// Enters the game loop of the GameWindow using the maximum update rate.
        /// </summary>
        /// <seealso cref="Run(double)"/>
        public void Run()
        {
            Run(0.0, 0.0);
        }

        /// <summary>
        /// Enters the game loop of the GameWindow using the specified update rate.
        /// maximum possible render frequency.
        /// </summary>
        public void Run(double updateRate)
        {
            Run(updateRate, 0.0);
        }

        /// <summary>
        /// Enters the game loop of the GameWindow updating and rendering at the specified frequency.
        /// </summary>
        /// <remarks>
        /// When overriding the default game loop you should call ProcessEvents()
        /// to ensure that your GameWindow responds to operating system events.
        /// <para>
        /// Once ProcessEvents() returns, it is time to call update and render the next frame.
        /// </para>
        /// </remarks>
        /// <param name="updates_per_second">The frequency of UpdateFrame events.</param>
        /// <param name="frames_per_second">The frequency of RenderFrame events.</param>
        public void Run(double updates_per_second, double frames_per_second)
        {
            EnsureUndisposed();

            try
            {
                if (updates_per_second < 0.0 || updates_per_second > 200.0)
                {
                    throw new ArgumentOutOfRangeException("updates_per_second", updates_per_second,
                        "Parameter should be inside the range [0.0, 200.0]");
                }
                if (frames_per_second < 0.0 || frames_per_second > 200.0)
                {
                    throw new ArgumentOutOfRangeException("frames_per_second", frames_per_second,
                        "Parameter should be inside the range [0.0, 200.0]");
                }

                if (updates_per_second != 0)
                {
                    TargetUpdateFrequency = updates_per_second;
                }
                if (frames_per_second != 0)
                {
                    TargetRenderFrequency = frames_per_second;
                }

                Visible = true;   // Make sure the GameWindow is visible.
                OnLoadInternal(EventArgs.Empty);
                OnResize(EventArgs.Empty);

                // On some platforms, ProcessEvents() does not return while the user is resizing or moving
                // the window. We can avoid this issue by raising UpdateFrame and RenderFrame events
                // whenever we encounter a size or move event.
                // Note: hack disabled. Threaded rendering provides a better solution to this issue.
                //Move += DispatchUpdateAndRenderFrame;
                //Resize += DispatchUpdateAndRenderFrame;

                Debug.Print("Entering main loop.");
                updateThread = new Thread(UpdateThread);
                updateThread.Start();
                watchRender.Start();
                while (true)
                {
                    ProcessEvents();
                    if (Exists && !IsExiting)
                    {
                        DispatchRenderFrame();
                    }
                    else
                    {
                        return;
                    }
                }
            }
            finally
            {
                if (Exists)
                {
                    // TODO: Should similar behaviour be retained, possibly on native window level?
                    //while (this.Exists)
                    //    ProcessEvents(false);
                }
            }
        }

        private void UpdateThread()
        {
            OnUpdateThreadStarted(this, new EventArgs());
            watchUpdate.Start();
            while (Exists && !IsExiting)
            {
                DispatchUpdateFrame(watchUpdate);
            }
        }

        private double ClampElapsed(double elapsed)
        {
            return MathHelper.Clamp(elapsed, 0.0, 1.0);
        }

        private void DispatchUpdateFrame(Stopwatch watch)
        {
            double timestamp = watch.Elapsed.TotalSeconds;
            double elapsed = ClampElapsed(timestamp - update_timestamp);

            RaiseUpdateFrame(watch, elapsed, ref timestamp);

            double delta = watch.Elapsed.TotalSeconds - timestamp;
            var toWait = (int)Math.Max(0, TargetUpdatePeriod * 1000 - (timestamp - update_timestamp) * 1000 - delta * 1000);

            Thread.Sleep(toWait);
        }

        private void DispatchRenderFrame()
        {
            double timestamp = watchRender.Elapsed.TotalSeconds;
            double elapsed = ClampElapsed(timestamp - render_timestamp);

            RaiseRenderFrame(elapsed, ref timestamp);

            //double delta = watchRender.Elapsed.TotalSeconds - timestamp;
            var toWait = Math.Max(0, TargetRenderPeriod * 1000 - elapsed * 1000);

            //var t = watchRender.Elapsed.TotalSeconds;
            if (toWait > 0)
                Thread.Sleep(TimeSpan.FromMilliseconds(toWait));
            //Console.WriteLine(toWait);
            //var over = watchRender.Elapsed.TotalSeconds - t;

           //render_last_overwait = over * 1000 - toWait;
        }

        private void RaiseUpdateFrame(Stopwatch watch, double elapsed, ref double timestamp)
        {
            // Raise UpdateFrame event
            update_args.Time = elapsed;
            OnUpdateFrameInternal(update_args);

            // Update UpdatePeriod/UpdateFrequency properties
            update_period = elapsed;

            // Update UpdateTime property
            update_timestamp = timestamp;
            timestamp = watch.Elapsed.TotalSeconds;
            update_time = timestamp - update_timestamp;
        }


        private void RaiseRenderFrame(double elapsed, ref double timestamp)
        {
            // Raise RenderFrame event
            render_args.Time = elapsed;
            OnRenderFrameInternal(render_args);

            // Update RenderPeriod/UpdateFrequency properties
            render_period = elapsed;

            // Update RenderTime property
            render_timestamp = timestamp;
            timestamp = watchRender.Elapsed.TotalSeconds;
            render_time = timestamp - render_timestamp;
        }

        /// <summary>
        /// Swaps the front and back buffer, presenting the rendered scene to the user.
        /// </summary>
        public void SwapBuffers()
        {
            EnsureUndisposed();
            this.Context.SwapBuffers();
        }

        /// <summary>
        /// Returns the opengl IGraphicsContext associated with the current GameWindow.
        /// </summary>
        public IGraphicsContext Context
        {
            get
            {
                EnsureUndisposed();
                return glContext;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the shutdown sequence has been initiated
        /// for this window, by calling GameWindow.Exit() or hitting the 'close' button.
        /// If this property is true, it is no longer safe to use any OpenTK.Input or
        /// OpenTK.Graphics.OpenGL functions or properties.
        /// </summary>
        public bool IsExiting
        {
            get
            {
                EnsureUndisposed();
                return isExiting;
            }
        }

        // TODO: Disabled because it is not reliable enough. Use vsync as a workaround.

        //public bool AllowSleep
        //{
        //    get { return allow_sleep; }
        //    set { allow_sleep = value; }
        //}

        /// <summary>
        /// Gets a double representing the actual frequency of RenderFrame events, in hertz (i.e. fps or frames per second).
        /// </summary>
        public double RenderFrequency
        {
            get
            {
                EnsureUndisposed();
                if (render_period == 0.0)
                {
                    return 1.0;
                }
                return 1.0 / render_period;
            }
        }

        /// <summary>
        /// Gets a double representing the period of RenderFrame events, in seconds.
        /// </summary>
        public double RenderPeriod
        {
            get
            {
                EnsureUndisposed();
                return render_period;
            }
        }

        /// <summary>
        /// Gets a double representing the time spent in the RenderFrame function, in seconds.
        /// </summary>
        public double RenderTime
        {
            get
            {
                EnsureUndisposed();
                return render_time;
            }
            protected set
            {
                EnsureUndisposed();
                render_time = value;
            }
        }

        /// <summary>
        /// Gets or sets a double representing the target render frequency, in hertz.
        /// </summary>
        /// <remarks>
        /// <para>A value of 0.0 indicates that RenderFrame events are generated at the maximum possible frequency (i.e. only limited by the hardware's capabilities).</para>
        /// <para>Values lower than 1.0Hz are clamped to 0.0. Values higher than 500.0Hz are clamped to 200.0Hz.</para>
        /// </remarks>
        public double TargetRenderFrequency
        {
            get
            {
                EnsureUndisposed();
                if (TargetRenderPeriod == 0.0)
                {
                    return 0.0;
                }
                return 1.0 / TargetRenderPeriod;
            }
            set
            {
                EnsureUndisposed();
                if (value < 1.0)
                {
                    TargetRenderPeriod = 0.0;
                }
                else if (value <= MaxFrequency)
                {
                    TargetRenderPeriod = 1.0 / value;
                }
                else
                {
                    Debug.Print("Target render frequency clamped to {0}Hz.", MaxFrequency);
                }
            }
        }

        /// <summary>
        /// Gets or sets a double representing the target render period, in seconds.
        /// </summary>
        /// <remarks>
        /// <para>A value of 0.0 indicates that RenderFrame events are generated at the maximum possible frequency (i.e. only limited by the hardware's capabilities).</para>
        /// <para>Values lower than 0.002 seconds (500Hz) are clamped to 0.0. Values higher than 1.0 seconds (1Hz) are clamped to 1.0.</para>
        /// </remarks>
        public double TargetRenderPeriod
        {
            get
            {
                EnsureUndisposed();
                return target_render_period;
            }
            set
            {
                EnsureUndisposed();
                if (value <= 1 / MaxFrequency)
                {
                    target_render_period = 0.0;
                }
                else if (value <= 1.0)
                {
                    target_render_period = value;
                }
                else
                {
                    Debug.Print("Target render period clamped to 1.0 seconds.");
                }
            }
        }

        /// <summary>
        /// Gets or sets a double representing the target update frequency, in hertz.
        /// </summary>
        /// <remarks>
        /// <para>A value of 0.0 indicates that UpdateFrame events are generated at the maximum possible frequency (i.e. only limited by the hardware's capabilities).</para>
        /// <para>Values lower than 1.0Hz are clamped to 0.0. Values higher than 500.0Hz are clamped to 500.0Hz.</para>
        /// </remarks>
        public double TargetUpdateFrequency
        {
            get
            {
                EnsureUndisposed();
                if (TargetUpdatePeriod == 0.0)
                {
                    return 0.0;
                }
                return 1.0 / TargetUpdatePeriod;
            }
            set
            {
                EnsureUndisposed();
                if (value < 1.0)
                {
                    TargetUpdatePeriod = 0.0;
                }
                else if (value <= MaxFrequency)
                {
                    TargetUpdatePeriod = 1.0 / value;
                }
                else
                {
                    Debug.Print("Target render frequency clamped to {0}Hz.", MaxFrequency);
                }
            }
        }

        /// <summary>
        /// Gets or sets a double representing the target update period, in seconds.
        /// </summary>
        /// <remarks>
        /// <para>A value of 0.0 indicates that UpdateFrame events are generated at the maximum possible frequency (i.e. only limited by the hardware's capabilities).</para>
        /// <para>Values lower than 0.002 seconds (500Hz) are clamped to 0.0. Values higher than 1.0 seconds (1Hz) are clamped to 1.0.</para>
        /// </remarks>
        public double TargetUpdatePeriod
        {
            get
            {
                EnsureUndisposed();
                return target_update_period;
            }
            set
            {
                EnsureUndisposed();
                if (value <= 1 / MaxFrequency)
                {
                    target_update_period = 0.0;
                }
                else if (value <= 1.0)
                {
                    target_update_period = value;
                }
                else
                {
                    Debug.Print("Target update period clamped to 1.0 seconds.");
                }
            }
        }

        /// <summary>
        /// Gets a double representing the frequency of UpdateFrame events, in hertz.
        /// </summary>
        public double UpdateFrequency
        {
            get
            {
                EnsureUndisposed();
                if (update_period == 0.0)
                {
                    return 1.0;
                }
                return 1.0 / update_period;
            }
        }

        /// <summary>
        /// Gets a double representing the period of UpdateFrame events, in seconds.
        /// </summary>
        public double UpdatePeriod
        {
            get
            {
                EnsureUndisposed();
                return update_period;
            }
        }

        /// <summary>
        /// Gets a double representing the time spent in the UpdateFrame function, in seconds.
        /// </summary>
        public double UpdateTime
        {
            get
            {
                EnsureUndisposed();
                return update_time;
            }
        }

        /// <summary>
        /// Gets or sets the VSyncMode.
        /// </summary>
        public VSyncMode VSync
        {
            get
            {
                EnsureUndisposed();
                GraphicsContext.Assert();
                if (Context.SwapInterval < 0)
                {
                    return VSyncMode.Adaptive;
                }
                else if (Context.SwapInterval == 0)
                {
                    return VSyncMode.Off;
                }
                else
                {
                    return VSyncMode.On;
                }
            }
            set
            {
                EnsureUndisposed();
                GraphicsContext.Assert();
                switch (value)
                {
                    case VSyncMode.On:
                        Context.SwapInterval = 1;
                        break;

                    case VSyncMode.Off:
                        Context.SwapInterval = 0;
                        break;

                    case VSyncMode.Adaptive:
                        Context.SwapInterval = -1;
                        break;
                }
            }
        }

        /// <summary>
        /// Gets or states the state of the NativeWindow.
        /// </summary>
        public override WindowState WindowState
        {
            get
            {
                return base.WindowState;
            }
            set
            {
                base.WindowState = value;
                Debug.Print("Updating Context after setting WindowState to {0}", value);

                if (Context != null)
                {
                    Context.Update(WindowInfo);
                }
            }
        }
        /// <summary>
        /// Occurs before the window is displayed for the first time.
        /// </summary>
        public event EventHandler<EventArgs> Load = delegate { };

        /// <summary>
        /// Occurs when it is time to render a frame.
        /// </summary>
        public event EventHandler<FrameArgs> RenderFrame = delegate { };

        /// <summary>
        /// Occurs before the window is destroyed.
        /// </summary>
        public event EventHandler<EventArgs> Unload = delegate { };

        /// <summary>
        /// Occurs when it is time to update a frame.
        /// </summary>
        public event EventHandler<FrameArgs> UpdateFrame = delegate { };

        /// <summary>
        /// If game window is configured to run with a dedicated update thread (by passing isSingleThreaded = false in the constructor),
        /// occurs when the update thread has started. This would be a good place to initialize thread specific stuff (like setting a synchronization context).
        /// </summary>
        public event EventHandler OnUpdateThreadStarted = delegate { };

        /// <summary>
        /// Override to add custom cleanup logic.
        /// </summary>
        /// <param name="manual">True, if this method was called by the application; false if this was called by the finalizer thread.</param>
        protected virtual void Dispose(bool manual) { }

        /// <summary>
        /// Called when the frame is rendered.
        /// </summary>
        /// <param name="e">Contains information necessary for frame rendering.</param>
        /// <remarks>
        /// Subscribe to the <see cref="RenderFrame"/> event instead of overriding this method.
        /// </remarks>
        protected virtual void OnRenderFrame(FrameArgs e)
        {
            RenderFrame(this, e);
        }

        /// <summary>
        /// Called when the frame is updated.
        /// </summary>
        /// <param name="e">Contains information necessary for frame updating.</param>
        /// <remarks>
        /// Subscribe to the <see cref="UpdateFrame"/> event instead of overriding this method.
        /// </remarks>
        protected virtual void OnUpdateFrame(FrameArgs e)
        {
            UpdateFrame(this, e);
        }

        /// <summary>
        /// Called when the WindowInfo for this GameWindow has changed.
        /// </summary>
        /// <param name="e">Not used.</param>
        protected virtual void OnWindowInfoChanged(EventArgs e) { }

        /// <summary>
        /// Called when this window is resized.
        /// </summary>
        /// <param name="e">Not used.</param>
        /// <remarks>
        /// You will typically wish to update your viewport whenever
        /// the window is resized. See the
        /// <see cref="OpenTK.Graphics.OpenGL.GL.Viewport(int, int, int, int)"/> method.
        /// </remarks>
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            glContext.Update(base.WindowInfo);
        }

        private void OnLoadInternal(EventArgs e)
        {
            OnLoad(e);
        }

        private void OnRenderFrameInternal(FrameArgs e)
        {
            if (Exists && !isExiting)
            {
                OnRenderFrame(e);
            }
        }

        private void OnUnloadInternal(EventArgs e)
        {
            OnUnload(e);
        }

        private void OnUpdateFrameInternal(FrameArgs e)
        {
            if (Exists && !isExiting)
            {
                OnUpdateFrame(e);
            }
        }

        private void OnWindowInfoChangedInternal(EventArgs e)
        {
            glContext.MakeCurrent(WindowInfo);

            OnWindowInfoChanged(e);
        }
    }
}
