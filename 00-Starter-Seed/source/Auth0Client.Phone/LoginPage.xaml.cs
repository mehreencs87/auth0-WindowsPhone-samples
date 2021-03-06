﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Phone.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkID=390556

namespace Auth0.SDK
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    internal sealed partial class LoginPage : Page
    {
        private const string NoDetailsAvailableMessage = "No details available.";
        private string responseData = string.Empty;
        private string responseErrorDetail = string.Empty;
        private PhoneAuthenticationStatus responseStatus = PhoneAuthenticationStatus.UserCancel;

        // We need to keep this state to make sure we do the right thing even during
        // normal phone navigation actions (such as going to start screen and back).
        private bool authenticationStarted = false;
        private bool authenticationFinished = false;

        /// <summary>
        /// The AuthenticationBroker associated with the current Login action.
        /// </summary>
        internal AuthenticationBroker Broker { get; set; }

        /// <summary>
        /// Initiatlizes the page by hooking up some event handlers.
        /// </summary>
        public LoginPage()
        {
            InitializeComponent();
            BrowserControl.NavigationStarting += BrowserControl_NavigationStarting;
            BrowserControl.LoadCompleted += BrowserControl_LoadCompleted;
            BrowserControl.NavigationFailed += BrowserControl_NavigationFailed;
        }

        private void UnhookEvents()
        {
            BrowserControl.NavigationStarting -= BrowserControl_NavigationStarting;
            BrowserControl.LoadCompleted -= BrowserControl_LoadCompleted;
            BrowserControl.NavigationFailed -= BrowserControl_NavigationFailed;
        }

        public WebView BrowserControl 
        { 
            get
            { 
                return this.browserControl;
            } 
        }


        /// <summary>
        /// Handler for the browser control's load completed event.  We use this to detect when
        /// to hide the progress bar and show the browser control.
        /// </summary>
        void BrowserControl_LoadCompleted(object sender, NavigationEventArgs e)
        {
            HideProgressBar();
        }

        /// <summary>
        /// Initiates the authentication operation by pointing the browser control
        /// to the PhoneWebAuthenticationBroker.StartUri.  If the PhoneWebAuthenticationBroker
        /// isn't currently in the middle of an authentication operation, then we immediately
        /// navigate back.
        /// </summary>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            HardwareButtons.BackPressed += LoginPage_BackKeyPress;
            base.OnNavigatedTo(e);

            // Make sure that there is an authentication operation in progress.
            // If not, we'll navigate back to the previous page.
            if (!Broker.AuthenticationInProgress)
            {
                this.Frame.GoBack();
            }

            if (!authenticationStarted)
            {
                authenticationStarted = true;
                authenticationFinished = false;

                // Point the browser control to the authentication start page.
                BrowserControl.Source = Broker.StartUri;
            }
        }

        /// <summary>
        /// Updates the PhoneWebAuthenticationBroker on the state of the authentication
        /// operation.  If we navigated back by pressing the back key, then the operation
        /// will be canceled.  If the browser control successfully completed the operation,
        /// signaled by its navigating to the PhoneWebAuthenticationBroker.EndUri, then we
        /// pass the results on.
        /// </summary>
        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            // If there is an active authentication operation in progress and we have
            // finished, then we need to inform the authentication broker of the results.
            // We don't want to stop the operation prematurely, such as when navigating to
            // the start screen.
            if (Broker.AuthenticationInProgress && authenticationFinished)
            {
                authenticationStarted = false;
                authenticationFinished = false;

                Broker.OnAuthenticationFinished(responseData, responseStatus, responseErrorDetail);
            }

            HardwareButtons.BackPressed -= LoginPage_BackKeyPress;
        }

        /// <summary>
        /// Handler for the page's back key events.  We use this to determine whether navigations
        /// away from this page are benign (such as going to the start screen) or actually meant
        /// to cancel the operation.
        /// </summary>
        private void LoginPage_BackKeyPress(object sender, BackPressedEventArgs e)
        {
            UnhookEvents();
            responseData = "";
            responseStatus = PhoneAuthenticationStatus.UserCancel;

            authenticationFinished = true;
            
            e.Handled = true;
            NavigateBackWithProgress();
        }


        /// <summary>
        /// Handler for the browser control's navigating event.  We use this to detect when login
        /// has completed.
        /// </summary>
        private void BrowserControl_NavigationStarting(object sender, WebViewNavigationStartingEventArgs e)
        {
            if (EqualsWithoutQueryString(e.Uri, Broker.EndUri))
            {
                if (e.Uri.Query.StartsWith("?error"))
                {
                    responseStatus = PhoneAuthenticationStatus.ErrorServer;
                    responseErrorDetail = NoDetailsAvailableMessage;
                    var match = Regex.Match(e.Uri.Query, @"\?error=([^&]+)&error_description=([^&]+).*", RegexOptions.None);
                    if (match.Success)
                    {
                        responseErrorDetail = string.Format("Error: {0}. Description: {1}",
                            WebUtility.UrlDecode(match.Groups[1].Value),
                            WebUtility.UrlDecode(match.Groups[2].Value));
                    }
                }
                else
                {
                    responseData = e.Uri.ToString();
                    responseStatus = PhoneAuthenticationStatus.Success;
                }

                authenticationFinished = true;

                UnhookEvents();

                // Navigate back now.
                this.NavigateBackWithProgress();
            }
        }

        /// <summary>
        /// Compares to URIs without taking the Query into account.
        /// </summary>
        /// <param name="uri">One of the URIs to compare.</param>
        /// <param name="otherUri">The other URI to use in the comparison.</param>
        /// <returns>True if the URIs are equal (except for the query), false otherwise.</returns>
        private bool EqualsWithoutQueryString(Uri uri, Uri otherUri)
        {
            return uri.AbsolutePath == otherUri.AbsolutePath
                            && uri.Host == otherUri.Host
                            && uri.Scheme == otherUri.Scheme;
        }

        /// <summary>
        /// Handler for the browser control's navigation failed event.  We use this to detect errors
        /// </summary>
        private void BrowserControl_NavigationFailed(object sender, WebViewNavigationFailedEventArgs e)
        {
            UnhookEvents();

            // Pass along the provided error information.
            responseErrorDetail = string.Format("Error: {0}", e.WebErrorStatus);
            responseStatus = PhoneAuthenticationStatus.ErrorHttp;

            authenticationFinished = true;

            // Navigate back now.
            this.NavigateBackWithProgress();
        }

        /// <summary>
        /// Displays the progress bar and navigates to the previous page.
        /// </summary>
        private void NavigateBackWithProgress()
        {
            ShowProgressBar();
            this.Frame.GoBack();
        }

        /// <summary>
        /// Shows the progress bar and hides the browser control.
        /// </summary>
        private void ShowProgressBar()
        {
            BrowserControl.Visibility = Visibility.Collapsed;
            progress.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Hides the progress bar and shows the browser control.
        /// </summary>
        private void HideProgressBar()
        {
            BrowserControl.Visibility = Visibility.Visible;
            progress.Visibility = Visibility.Collapsed;
        }
    }
}
