// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Microsoft.Toolkit.Win32.UI.Interop.WPF
{
    /// <summary>
    /// WindowsXamlHost control hosts UWP XAML content inside the Windows Presentation Foundation
    /// </summary>
    public partial class WindowsXamlHostBase : HwndHost
    {
        /// <summary>
        /// UWP XAML Application instance and root UWP XamlMetadataProvider.  Custom implementation required to
        /// probe at runtime for custom UWP XAML type information.  This must be created before
        /// creating any DesktopWindowXamlSource instances if custom UWP XAML types are required.
        /// </summary>
        [ThreadStatic]
        private readonly Windows.UI.Xaml.Application _application;

        /// <summary>
        /// UWP XAML DesktopWindowXamlSource instance that hosts XAML content in a win32 application
        /// </summary>
        protected Windows.UI.Xaml.Hosting.DesktopWindowXamlSource desktopWindowXamlSource;

        /// <summary>
        /// A reference count on the UWP XAML framework is tied to WindowsXamlManager's
        /// lifetime.  UWP XAML is spun up on the first WindowsXamlManager creation and
        /// deinitialized when the last instance of WindowsXamlManager is destroyed.
        /// </summary>
        private Windows.UI.Xaml.Hosting.WindowsXamlManager _windowsXamlManager;

        /// <summary>
        ///    Root UWP XAML content attached to WindowsXamlHost
        /// </summary>
        protected Windows.UI.Xaml.UIElement xamlRoot;

        /// <summary>
        /// Initializes a new instance of the WindowsXamlHost class: default constructor is required for use in WPF markup.
        /// (When the default constructor is called, object properties have not been set. Put WPF logic in OnInitialized.)
        /// </summary>
        public WindowsXamlHostBase()
        {
            // Create a custom UWP XAML Application object that implements reflection-based XAML metdata probing.
            // Instantiation of the application object must occur before creating the DesktopWindowXamlSource instance.
            // DesktopWindowXamlSource will create a generic Application object unable to load custom UWP XAML metadata.
            if (_application == null)
            {
                try
                {
                    // global::Windows.UI.Xaml.Application.Current may throw if DXamlCore has not been initialized.
                    // Treat the exception as an uninitialized global::Windows.UI.Xaml.Application condition.
                    _application = Windows.UI.Xaml.Application.Current as XamlApplication;
                }
                catch
                {
                    _application = new XamlApplication();
                }
            }

            // Create an instance of the WindowsXamlManager. This initializes and holds a
            // reference on the UWP XAML DXamlCore and must be explicitly created before
            // any UWP XAML types are programmatically created.  If WindowsXamlManager has
            // not been created before creating DesktopWindowXamlSource, DesktopWindowXaml source
            // will create an instance of WindowsXamlManager internally.  (Creation is explicit
            // here to illustrate how to initialize UWP XAML before initializing the DesktopWindowXamlSource.)
            _windowsXamlManager = Windows.UI.Xaml.Hosting.WindowsXamlManager.InitializeForCurrentThread();

            // Create DesktopWindowXamlSource, host for UWP XAML content
            desktopWindowXamlSource = new Windows.UI.Xaml.Hosting.DesktopWindowXamlSource();

            // Hook OnTakeFocus event for Focus processing
            desktopWindowXamlSource.TakeFocusRequested += OnTakeFocusRequested;
        }

        /// <summary>
        /// Gets or sets the root UWP XAML element displayed in the WPF control instance.  This UWP XAML element is
        /// the root element of the wrapped DesktopWindowXamlSource.
        /// </summary>
        public Windows.UI.Xaml.UIElement XamlRootInternal
        {
            get => xamlRoot;

            set => xamlRoot = value;
        }

        /// <summary>
        /// Has this wrapper control instance been disposed?
        /// </summary>
        private bool IsDisposed { get; set; }

        /// <summary>
        /// Creates global::Windows.UI.Xaml.Application object, wrapped DesktopWindowXamlSource instance; creates and
        /// sets root UWP XAML element on DesktopWindowXamlSource.
        /// </summary>
        /// <param name="hwndParent">Parent window handle</param>
        /// <returns>Handle to XAML window</returns>
        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            // 'EnableMouseInPointer' is called by the WindowsXamlManager during initialization. No need
            // to call it directly here.

            // Create DesktopWindowXamlSource instance
            var desktopWindowXamlSourceNative = desktopWindowXamlSource.GetInterop();

            // Associate the window where UWP XAML will display content
            desktopWindowXamlSourceNative.AttachToWindow(hwndParent.Handle);

            var windowHandle = desktopWindowXamlSourceNative.WindowHandle;

            // Overridden function must return window handle of new target window (DesktopWindowXamlSource's Window)
            return new HandleRef(this, windowHandle);
        }

        /// <summary>
        /// WPF framework request to destroy control window.  Cleans up the HwndIslandSite created by DesktopWindowXamlSource
        /// </summary>
        /// <param name="hwnd">Handle of window to be destroyed</param>
        protected override void DestroyWindowCore(HandleRef hwnd)
        {
            Dispose(true);
        }

        /// <summary>
        /// WindowsXamlHost Dispose
        /// </summary>
        /// <param name="disposing">Is disposing?</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && !IsDisposed)
            {
                IsDisposed = true;
                desktopWindowXamlSource.TakeFocusRequested -= OnTakeFocusRequested;
                xamlRoot = null;
                desktopWindowXamlSource.Dispose();
                desktopWindowXamlSource = null;
            }
        }
    }
}
