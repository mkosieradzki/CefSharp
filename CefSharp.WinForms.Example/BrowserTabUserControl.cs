﻿using CefSharp.Example;
using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using CefSharp.WinForms.Internals;

namespace CefSharp.WinForms.Example
{
    public partial class BrowserTabUserControl : UserControl
    {
        public IWinFormsWebBrowser Browser { get; private set; }

        public BrowserTabUserControl(string url)
        {
            InitializeComponent();

            var browser = new ChromiumWebBrowser(url)
            {
                Dock = DockStyle.Fill
            };

            browserPanel.Controls.Add(browser);

            Browser = browser;

            browser.MenuHandler = new MenuHandler();
            browser.RequestHandler = new RequestHandler();
            browser.JsDialogHandler = new JsDialogHandler();
            browser.GeolocationHandler = new GeolocationHandler();
            browser.DownloadHandler = new DownloadHandler();
            browser.KeyboardHandler = new KeyboardHandler();
            //browser.FocusHandler = new FocusHandler(browser, urlTextBox);
            browser.LoadingStateChanged += OnBrowserLoadingStateChanged;
            browser.ConsoleMessage += OnBrowserConsoleMessage;
            browser.TitleChanged += OnBrowserTitleChanged;
            browser.AddressChanged += OnBrowserAddressChanged;
            browser.StatusMessage += OnBrowserStatusMessage;
            browser.IsBrowserInitializedChanged += OnIsBrowserInitializedChanged;
            browser.LoadError += OnLoadError;
            browser.DragHandler = new DragHandler();
            browser.RegisterJsObject("bound", new BoundObject());

            CefExample.RegisterTestResources(browser);

            var version = String.Format("Chromium: {0}, CEF: {1}, CefSharp: {2}", Cef.ChromiumVersion, Cef.CefVersion, Cef.CefSharpVersion);
            DisplayOutput(version);
        }

        private void OnLoadError(object sender, LoadErrorEventArgs args)
        {
            DisplayOutput("Load Error:" + args.ErrorCode + ";" + args.ErrorText);
        }

        private void OnBrowserConsoleMessage(object sender, ConsoleMessageEventArgs args)
        {
            DisplayOutput(string.Format("Line: {0}, Source: {1}, Message: {2}", args.Line, args.Source, args.Message));
        }

        private void OnBrowserStatusMessage(object sender, StatusMessageEventArgs args)
        {
            this.InvokeOnUiThreadIfRequired(() => statusLabel.Text = args.Value);
        }

        private void OnBrowserLoadingStateChanged(object sender, LoadingStateChangedEventArgs args)
        {
            SetCanGoBack(args.CanGoBack);
            SetCanGoForward(args.CanGoForward);

            this.InvokeOnUiThreadIfRequired(() => SetIsLoading(!args.CanReload));
        }

        private void OnBrowserTitleChanged(object sender, TitleChangedEventArgs args)
        {
            this.InvokeOnUiThreadIfRequired(() => Parent.Text = args.Title);
        }

        private void OnBrowserAddressChanged(object sender, AddressChangedEventArgs args)
        {
            this.InvokeOnUiThreadIfRequired(() => urlTextBox.Text = args.Address);
        }

        private void SetCanGoBack(bool canGoBack)
        {
            this.InvokeOnUiThreadIfRequired(() => backButton.Enabled = canGoBack);
        }

        private void SetCanGoForward(bool canGoForward)
        {
            this.InvokeOnUiThreadIfRequired(() => forwardButton.Enabled = canGoForward);
        }

        private void SetIsLoading(bool isLoading)
        {
            goButton.Text = isLoading ?
                "Stop" :
                "Go";
            goButton.Image = isLoading ?
                Properties.Resources.nav_plain_red :
                Properties.Resources.nav_plain_green;

            HandleToolStripLayout();
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private void OnIsBrowserInitializedChanged(object sender, IsBrowserInitializedChangedEventArgs args)
        {
            if (args.IsBrowserInitialized)
            {
                ChromeWidgetMessageInterceptor.SetupLoop((ChromiumWebBrowser)Browser, (message) =>
                {
                    const int WM_MOUSEACTIVATE = 0x0021;
                    const int WM_NCLBUTTONDOWN = 0x00A1;

                    if (message.Msg == WM_MOUSEACTIVATE) {
                        // The default processing of WM_MOUSEACTIVATE results in MA_NOACTIVATE,
                        // and the subsequent mouse click is eaten by Chrome.
                        // This means any .NET ToolStrip or ContextMenuStrip does not get closed.
                        // By posting a WM_NCLBUTTONDOWN message to a harmless co-ordinate of the
                        // top-level window, we rely on the ToolStripManager's message handling
                        // to close any open dropdowns:
                        // http://referencesource.microsoft.com/#System.Windows.Forms/winforms/Managed/System/WinForms/ToolStripManager.cs,1249
                        var topLevelWindowHandle = message.WParam;
                        PostMessage(topLevelWindowHandle, WM_NCLBUTTONDOWN, IntPtr.Zero, IntPtr.Zero);
                    }

                    // The ChromiumWebBrowserControl does not fire MouseEnter/Move/Leave events, because Chromium handles these.
                    // However we can hook into Chromium's messaging window to receive the events.
                    //
                    //const int WM_MOUSEMOVE = 0x0200;
                    //const int WM_MOUSELEAVE = 0x02A3;
                    //
                    //switch (message.Msg) {
                    //    case WM_MOUSEMOVE:
                    //        Console.WriteLine("WM_MOUSEMOVE");
                    //        break;
                    //    case WM_MOUSELEAVE:
                    //        Console.WriteLine("WM_MOUSELEAVE");
                    //        break;
                    //}
                });
            }
        }

        public void ExecuteScript(string script)
        {
            Browser.ExecuteScriptAsync(script);
        }

        public object EvaluateScript(string script)
        {
            var task = Browser.EvaluateScriptAsync(script);
            task.Wait();
            return task.Result;
        }

        public void DisplayOutput(string output)
        {
            this.InvokeOnUiThreadIfRequired(() => outputLabel.Text = output);
        }

        private void HandleToolStripLayout(object sender, LayoutEventArgs e)
        {
            HandleToolStripLayout();
        }

        private void HandleToolStripLayout()
        {
            var width = toolStrip1.Width;
            foreach (ToolStripItem item in toolStrip1.Items)
            {
                if (item != urlTextBox)
                {
                    width -= item.Width - item.Margin.Horizontal;
                }
            }
            urlTextBox.Width = Math.Max(0, width - urlTextBox.Margin.Horizontal - 18);
        }

        private void GoButtonClick(object sender, EventArgs e)
        {
            LoadUrl(urlTextBox.Text);
        }

        private void BackButtonClick(object sender, EventArgs e)
        {
            Browser.Back();
        }

        private void ForwardButtonClick(object sender, EventArgs e)
        {
            Browser.Forward();
        }

        private void UrlTextBoxKeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            LoadUrl(urlTextBox.Text);
        }

        private void LoadUrl(string url)
        {
            if (Uri.IsWellFormedUriString(url, UriKind.RelativeOrAbsolute))
            {
                Browser.Load(url);
            }
        }

        public async void CopySourceToClipBoardAsync()
        {
            var htmlSource = await Browser.GetSourceAsync();

            Clipboard.SetText(htmlSource);
            DisplayOutput("HTML Source copied to clipboard");
        }

        private void ToggleBottomToolStrip()
        {
            if (toolStrip2.Visible)
            {
                Browser.StopFinding(true);
                toolStrip2.Visible = false;
            }
            else
            {
                toolStrip2.Visible = true;
                findTextBox.Focus();
            }
        }

        private void FindNextButtonClick(object sender, EventArgs e)
        {
            Find(true);
        }

        private void FindPreviousButtonClick(object sender, EventArgs e)
        {
            Find(false);
        }

        private void Find(bool next)
        {
            if (!string.IsNullOrEmpty(findTextBox.Text))
            {
                Browser.Find(0, findTextBox.Text, next, false, false);
            }
        }

        private void FindTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            Find(true);
        }

        public void ShowFind()
        {
            ToggleBottomToolStrip();
        }

        private void FindCloseButtonClick(object sender, EventArgs e)
        {
            ToggleBottomToolStrip();
        }
    }
}
