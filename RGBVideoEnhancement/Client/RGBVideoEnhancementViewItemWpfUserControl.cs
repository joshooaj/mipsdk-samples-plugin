﻿using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Media.Imaging;
using VideoOS.Platform;
using VideoOS.Platform.Client;
using VideoOS.Platform.Data;
using VideoOS.Platform.Live;
using VideoOS.Platform.Messaging;

namespace RGBVideoEnhancement.Client
{
    public partial class RGBVideoEnhancementViewItemWpfUserControl : ViewItemWpfUserControl
    {
        #region Component private class variables

        private readonly RGBVideoEnhancementViewItemManager _viewItemManager;

        private Item _selectedItem = null;
        private BitmapVideoSource _bitmapVideoSource;
        private BitmapVideoSource _bitmapVideoSourceNext;
        private readonly object _bitmapVideoSourceNextLock = new object();

        private BitmapLiveSource _bitmapLiveSource;

        private string _mode = PlaybackPlayModeData.Stop;
        private DateTime _currentShownTime = DateTime.MinValue;
        private bool _redisplay;
        private bool _stopPlayback;
        private bool _stopLive;

        private object _objRef1, _objRef2, _objRef3, _objRef4;

        private ToolkitRGBEnhancement.RGBHandling.Transform _transform;

        private readonly Bitmap _blackImage = new Bitmap(1, 1);

        private MyPlayCommand _nextCommand = MyPlayCommand.None;

        enum MyPlayCommand
        {
            None,
            Start,
            End,
            NextSequence,
            PrevSequence,
            NextFrame,
            PrevFrame
        }

        #endregion

        #region Component constructors + dispose

        /// <summary>
        /// Constructs a RGBVideoEnhancementViewItemWpfUserControl instance
        /// </summary>
        public RGBVideoEnhancementViewItemWpfUserControl(RGBVideoEnhancementViewItemManager viewItemManager)
        {
            _viewItemManager = viewItemManager;

            InitializeComponent();

            Graphics g = Graphics.FromImage(_blackImage);
            g.FillRectangle(System.Drawing.Brushes.Black, 0, 0, 1, 1);
            g.Dispose();

        }

        private void SetUpApplicationEventListeners()
        {
            //set up ViewItem event listeners
            _viewItemManager.PropertyChangedEvent += ViewItemManagerPropertyChangedEvent;

            _objRef1 = EnvironmentManager.Instance.RegisterReceiver(PlaybackTimeChangedHandler,
                                             new PlaybackControllerFilter(MessageId.SmartClient.PlaybackCurrentTimeIndication, WindowInformation));
            _objRef2 = EnvironmentManager.Instance.RegisterReceiver(PlaybackCommandHandler,
                                             new PlaybackControllerFilter(MessageId.SmartClient.PlaybackIndication, WindowInformation));
            _objRef3 = EnvironmentManager.Instance.RegisterReceiver(ApplicationModeChangedMessage,
                                                         new WindowFilter(MessageId.System.ModeChangedIndication, WindowInformation));
            _objRef4 = EnvironmentManager.Instance.RegisterReceiver(ThemeChangedIndicationHandler,
                                                         new MessageIdFilter(MessageId.SmartClient.ThemeChangedIndication));
        }

        private void RemoveApplicationEventListeners()
        {
            //remove ViewItem event listeners
            _viewItemManager.PropertyChangedEvent -= ViewItemManagerPropertyChangedEvent;
            EnvironmentManager.Instance.UnRegisterReceiver(_objRef1);
            _objRef1 = null;
            EnvironmentManager.Instance.UnRegisterReceiver(_objRef2);
            _objRef2 = null;
            EnvironmentManager.Instance.UnRegisterReceiver(_objRef3);
            _objRef3 = null;
            EnvironmentManager.Instance.UnRegisterReceiver(_objRef4);
            _objRef4 = null;
        }

        /// <summary>
        /// Method that is called immediately after the view item is displayed.
        /// </summary>
        public override void Init()
        {
            SetUpApplicationEventListeners();

            _transform = new ToolkitRGBEnhancement.RGBHandling.Transform();

            _fetchThread = new Thread(BitmapFetchThread);
            _fetchThread.Start();

            OnScrollChange(null, null);

            ModeHandler(WindowInformation.Mode);
            if (_viewItemManager.SelectedCamera != null)
            {
                ViewItemManagerPropertyChangedEvent(null, null);
            }
        }

        public static System.Windows.Media.Imaging.BitmapSource ToWpfBitmap(Bitmap bitmap)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                bitmap.Save(stream, ImageFormat.Bmp);
                stream.Position = 0;
                BitmapImage result = new BitmapImage();
                result.BeginInit();
                result.CacheOption = BitmapCacheOption.OnLoad;
                result.StreamSource = stream;
                result.EndInit();
                result.Freeze();
                return result;
            }
        }

        /// <summary>
        /// Method that is called when the view item is closed. The view item should free all resources when the method is called.
        /// Is called when userControl is not displayed anymore. Either because of 
        /// user clicking on another View or Item has been removed from View.
        /// </summary>
        public override void Close()
        {
            _stop = true;
            _stopLive = true;
            _stopPlayback = true;

            CloseLiveSession();
            ClosePlaybackSession();

            RemoveApplicationEventListeners();

            if (imageBox.Width != 0 && imageBox.Height != 0)
                imageBox.Width = 1;
            imageBox.Height = 1;

        }

        public override bool ShowToolbar
        {
            get { return false; }
        }


        #endregion

        #region Time changed event handler


        private object PlaybackTimeChangedHandler(Message message, FQID dest, FQID sender)
        {
            var time = (DateTime)message.Data;
            _nextToFetchTime = time;
            return null;
        }

        // When the users presses the different buttons on the playback control, this method
        // will follow those instructions
        private object PlaybackCommandHandler(Message message, FQID dest, FQID sender)
        {
            var commandData = message.Data as PlaybackCommandData;
            if (commandData != null)
            {
                switch (commandData.Command)
                {
                    case PlaybackData.Begin:
                        _nextCommand = MyPlayCommand.Start;
                        break;
                    case PlaybackData.End:
                        _nextCommand = MyPlayCommand.End;
                        break;
                    case PlaybackData.Previous:
                        _nextCommand = MyPlayCommand.PrevFrame;
                        break;
                    case PlaybackData.Next:
                        _nextCommand = MyPlayCommand.NextFrame;
                        break;
                    case PlaybackData.PreviousSequence:
                        _nextCommand = MyPlayCommand.PrevSequence;
                        break;
                    case PlaybackData.NextSequence:
                        _nextCommand = MyPlayCommand.NextSequence;
                        break;
                    case PlaybackData.PlayForward:
                    case PlaybackData.Play:
                        _mode = PlaybackPlayModeData.Forward;
                        break;
                    case PlaybackData.PlayReverse:
                        _mode = PlaybackPlayModeData.Reverse;
                        break;
                    case PlaybackData.PlayStop:
                        _mode = PlaybackPlayModeData.Stop;
                        break;
                }
            }
            return null;
        }

        private object ApplicationModeChangedMessage(Message message, FQID destination, FQID sender)
        {
            ModeHandler((Mode)message.Data);
            return null;
        }

        private object ThemeChangedIndicationHandler(Message message, FQID destination, FQID source)
        {
            this.Selected = Selected;   // Reset the header line properties
            return null;
        }

        #endregion

        #region Video Session Open and Close

        private void ModeHandler(Mode newMode)
        {
            if (_bitmapVideoSource != null)
            {
                ClosePlaybackSession();
            }
            if (_bitmapLiveSource != null)
            {
                CloseLiveSession();
            }
            _mode = PlaybackPlayModeData.Stop;

            switch (newMode)
            {
                case Mode.ClientLive:
                    OpenLiveSession();
                    break;
                case Mode.ClientPlayback:
                    _mode = PlaybackPlayModeData.Forward;
                    OpenPlaybackSession();
                    break;
                case Mode.ClientSetup:
                    break;
            }
        }

        private void OpenPlaybackSession()
        {
            if (_viewItemManager.SelectedCamera != null)
            {
                _selectedItem = Configuration.Instance.GetItem(_viewItemManager.SelectedCamera.FQID);
                if (_selectedItem != null)
                {
                    labelCamera.Content = _selectedItem.Name;
                    lock (_bitmapVideoSourceNextLock)
                    {
                        _bitmapVideoSourceNext = new BitmapVideoSource(_selectedItem);
                        _bitmapVideoSourceNext.Width = _newWidth;
                        _bitmapVideoSourceNext.Height = _newHeight;
                        _bitmapVideoSourceNext.SetKeepAspectRatio(true, true);
                    }
                    _nextToFetchTime = DateTime.UtcNow;
                    _stopPlayback = false;
                }
            }
        }

        private void ClosePlaybackSession()
        {
            _stopPlayback = true;
        }

        private void OpenLiveSession()
        {
            if (_viewItemManager.SelectedCamera != null)
            {
                _selectedItem = Configuration.Instance.GetItem(_viewItemManager.SelectedCamera.FQID);
                if (_selectedItem != null)
                {
                    _bitmapLiveSource = new BitmapLiveSource(_selectedItem, BitmapFormat.BGR24);
                    _bitmapLiveSource.LiveContentEvent += BitmapLiveSourceLiveContentEvent;
                    _bitmapLiveSource.Width = _newWidth != 0 ? _newWidth : (int)imageBox.Width;
                    _bitmapLiveSource.Height = _newHeight != 0 ? _newHeight : (int)imageBox.Height;
                    _bitmapLiveSource.SetKeepAspectRatio(true, true);
                    _bitmapLiveSource.Init();
                    _bitmapLiveSource.LiveModeStart = true;
                    _bitmapLiveSource.SingleFrameQueue = true;		// New property from MIPSDK 2014
                    _stopLive = false;
                }
            }
        }

        private void CloseLiveSession()
        {
            _stopLive = true;
            if (_bitmapLiveSource != null)
            {
                _bitmapLiveSource.LiveContentEvent -= BitmapLiveSourceLiveContentEvent;
                _bitmapLiveSource.LiveModeStart = false;
                _bitmapLiveSource.Close();
                _bitmapLiveSource = null;
            }
        }

        #endregion

        #region Thread to get images in playback mode
        //
        // The primary purpose of this method is to have all calls in Playback mode beging executed on one thread per session,
        // and make sure it is not on the UI thread.

        private bool _stop;
        private Thread _fetchThread;
        private DateTime _nextToFetchTime;
        private PlaybackTimeInformationData _currentTimeInformation = null;
        private bool _requestInProgress = false;

        private void BitmapFetchThread()
        {
            bool errorRecovery = false;

            try
            {
                while (!_stop)
                {
                    if (_newWidth <= 0 || _newHeight <= 0)
                    {
                        continue;
                    }
                    try
                    {
                        if (_stopPlayback)
                        {
                            if (_bitmapVideoSource != null)
                            {
                                _bitmapVideoSource.Close();
                            }
                            _bitmapVideoSource = null;
                            _nextCommand = MyPlayCommand.None;
                            _nextToFetchTime = DateTime.MinValue;
                        }

                        if (_bitmapVideoSourceNext != null)
                        {
                            lock (_bitmapVideoSourceNextLock)
                            {
                                if (_bitmapVideoSource != null)
                                {
                                    _bitmapVideoSource.Close();
                                }
                                _bitmapVideoSource = _bitmapVideoSourceNext;
                                _bitmapVideoSourceNext = null;
                            }
                            _bitmapVideoSource.Init();
                            ShowError("");      // Clear messages
                            errorRecovery = false;
                        }

                        if (_bitmapVideoSource != null && _setResolution)
                        {
                            _bitmapVideoSource.Width = _newWidth;
                            _bitmapVideoSource.Height = _newHeight;
                            _bitmapVideoSource.SetWidthHeight();
                            _setResolution = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex is CommunicationMIPException)
                        {
                            ShowError("Connection lost to server ...");
                        }
                        else
                        {
                            ShowError(ex.Message);
                        }
                        errorRecovery = true;
                        _bitmapVideoSourceNext = _bitmapVideoSource;
                        _bitmapVideoSource = null;
                        _nextCommand = MyPlayCommand.None;
                        _nextToFetchTime = DateTime.MinValue;
                    }

                    if (errorRecovery)
                    {
                        Thread.Sleep(3000);
                        continue;
                    }

                    if (Selected && _nextCommand != MyPlayCommand.None && _bitmapVideoSource != null && !_requestInProgress)
                    {
                        try
                        {
                            VideoOS.Platform.Data.BitmapData bitmapData = null;
                            switch (_nextCommand)
                            {
                                case MyPlayCommand.Start:
                                    bitmapData = _bitmapVideoSource.GetBegin();
                                    break;
                                case MyPlayCommand.End:
                                    bitmapData = _bitmapVideoSource.GetEnd();
                                    break;
                                case MyPlayCommand.PrevSequence:
                                    bitmapData = _bitmapVideoSource.GetPreviousSequence();
                                    break;
                                case MyPlayCommand.NextSequence:
                                    bitmapData = _bitmapVideoSource.GetNextSequence();
                                    break;
                                case MyPlayCommand.PrevFrame:
                                    bitmapData = _bitmapVideoSource.GetPrevious();
                                    break;
                                case MyPlayCommand.NextFrame:
                                    bitmapData = _bitmapVideoSource.GetNext() as VideoOS.Platform.Data.BitmapData;
                                    break;
                            }
                            if (bitmapData != null)
                            {
                                ShowBitmap(bitmapData);
                                // Lets get the Smart Client to show this time, (This will issue a new set time command, but is same again (that we ignore))
                                EnvironmentManager.Instance.PostMessage(
                                    new Message(MessageId.SmartClient.PlaybackCommand,
                                        new PlaybackCommandData
                                        {
                                            Command = PlaybackData.Goto,
                                            DateTime = bitmapData.DateTime
                                        }));
                                bitmapData.Dispose();
                            }
                        }
                        catch (Exception ex)
                        {
                            if (ex is CommunicationMIPException)
                            {
                                ShowError("Connection lost to server ...");
                            }
                            else
                            {
                                ShowError(ex.Message);
                            }
                            errorRecovery = true;
                            _bitmapVideoSourceNext = _bitmapVideoSource;
                            _bitmapVideoSource = null;
                            continue;
                        }
                        _nextCommand = MyPlayCommand.None;
                    }

                    if (_nextToFetchTime != DateTime.MinValue &&
                        (_nextToFetchTime != _currentShownTime || _redisplay) &&
                        _bitmapVideoSource != null &&
                        !_requestInProgress)
                    {
                        DateTime time = _nextToFetchTime;
                        DateTime utcTime = time.Kind == DateTimeKind.Local ? time.ToUniversalTime() : time;

                        // Next 4 lines new for MIPSDK 2014
                        bool willResultInSameFrame = _currentTimeInformation != null &&
                                                     _currentTimeInformation.NextTime > utcTime &&
                                                     _currentTimeInformation.PreviousTime < utcTime;

                        // Let's validate if we are just asking for the same frame
                        _nextToFetchTime = DateTime.MinValue;

                        if (!willResultInSameFrame)
                        {
                            try
                            {
                                if (_mode == PlaybackPlayModeData.Stop)
                                {
                                    _bitmapVideoSource.GoTo(utcTime, PlaybackPlayModeData.Reverse);
                                }

                                VideoOS.Platform.Data.BitmapData bitmapData = (VideoOS.Platform.Data.BitmapData)_bitmapVideoSource.GetAtOrBefore(utcTime); // as System.Drawing.Imaging.BitmapData;

                                // For MIPSDK 2014
                                if (bitmapData != null && bitmapData.IsPreviousAvailable == false)
                                {
                                    if (utcTime - TimeSpan.FromMilliseconds(10) < bitmapData.DateTime)
                                        bitmapData.PreviousDateTime = utcTime - TimeSpan.FromMilliseconds(10);
                                    else
                                        bitmapData.PreviousDateTime = bitmapData.DateTime;
                                }

                                if (bitmapData != null)
                                {
                                    ShowBitmap(bitmapData);
                                    bitmapData.Dispose();
                                }
                            }
                            catch (Exception ex)
                            {
                                if (ex is CommunicationMIPException)
                                {
                                    ShowError("Connection to server lost ...");
                                }
                                else
                                {
                                    ShowError(ex.Message);
                                }
                                errorRecovery = true;
                                _bitmapVideoSourceNext = _bitmapVideoSource;
                                _bitmapVideoSource = null;
                            }
                        }
                    }
                    Thread.Sleep(10);
                }
            }
            catch (Exception ex)
            {
                EnvironmentManager.Instance.ExceptionHandler("RGBVideoEnhancement.PlaybackThread", ex);
            }

            if (_bitmapVideoSource != null)
            {
                _bitmapVideoSource.Close();
                _bitmapVideoSource = null;
            }
            _fetchThread = null;
        }

        /// <summary>
        /// New code as from MIPSDK 4.0 - to handle connection issues
        /// </summary>
        /// <param name="errorText"></param>
        private delegate void ShowErrorDelegate(String errorText);
        private void ShowError(String errorText)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new ShowErrorDelegate(ShowError), errorText);
            }
            else
            {
                Bitmap bitmap = new Bitmap((int)imageWindowContainer.ActualWidth, (int)imageWindowContainer.ActualHeight);
                Graphics g = Graphics.FromImage(bitmap);
                g.FillRectangle(System.Drawing.Brushes.Black, 0, 0, bitmap.Width, bitmap.Height);
                g.DrawString(errorText, new Font(System.Drawing.FontFamily.GenericMonospace, 12), System.Drawing.Brushes.White, new PointF(20, 100));
                g.Dispose();
                var imgSource = ToWpfBitmap(bitmap);
                imageBox.Source = imgSource;
                bitmap.Dispose();
            }
        }


        #endregion

        #region ShowBitmap in the UI thread

        private delegate void ShowBitmapDelegate(VideoOS.Platform.Data.BitmapData bitmapData);

        // The ShowBitmap now divided into 2 methods, for MIPSDK 2014
        private void ShowBitmap(VideoOS.Platform.Data.BitmapData bitmapData)
        {
            // Next 15 lines new for MIPSDK 2014
            if (_currentTimeInformation != null &&
                _currentTimeInformation.PreviousTime < bitmapData.DateTime &&
                _currentTimeInformation.NextTime > bitmapData.DateTime)
            {
                Debug.WriteLine("----> Duplicate bitmap at " + bitmapData.DateTime);	// this should only happen a few times during startup
                if (Selected)
                {
                    EnvironmentManager.Instance.SendMessage(new Message(
                        MessageId.SmartClient.PlaybackTimeInformation, _currentTimeInformation), null, _viewItemManager.FQID);
                }
                return;
            }

            // Set here to avoid race-condition. And we should use some locking to ensure thread-safety.
            _nextToFetchTime = bitmapData.DateTime;

            _currentTimeInformation = new PlaybackTimeInformationData
            {
                Item = _selectedItem.FQID,
                CurrentTime = bitmapData.DateTime,
                NextTime = bitmapData.NextDateTime,
                PreviousTime = bitmapData.PreviousDateTime
            };

            if (Selected)
            {
                EnvironmentManager.Instance.SendMessage(new Message(
                    MessageId.SmartClient.PlaybackTimeInformation, _currentTimeInformation), null, _viewItemManager.FQID);
            }

            _requestInProgress = true;
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new ShowBitmapDelegate(ShowBitmap2), bitmapData);
            }
            else
            {
                ShowBitmap2(bitmapData);
            }
        }

        private void ShowBitmap2(VideoOS.Platform.Data.BitmapData bitmapData)
        {
            {
                if (bitmapData.DateTime != _currentShownTime || _redisplay)
                {
                    _redisplay = false;

                    // The following code does these functions:
                    //    Get a IntPtr to the start of the GBR bitmap
                    //    Transform via sample transformation (To be replaced with your C++ code)
                    //    Create a Bitmap with the result
                    //    Create a new Bitmap scaled to visible area on screen
                    //    Assign new Bitmap into PictureBox
                    //    Dispose first Bitmap
                    //
                    // The transformation is therefore done on the original image, but if the transformation is
                    // keeping to the same size, then this would be much more effective if the resize was done first,
                    // and the transformation afterwards.
                    // Scaling can be done by setting the Width and Height on the 

                    int width = bitmapData.GetPlaneWidth(0);
                    int height = bitmapData.GetPlaneHeight(0);
                    int stride = bitmapData.GetPlaneStride(0);

                    // When using RGB / BGR bitmaps, they have all bytes continues in memory.  The PlanePointer(0) is used for all planes:
                    IntPtr plane0 = bitmapData.GetPlanePointer(0);

                    IntPtr newPlane0 = _transform.Perform(plane0, stride, width, height);		// Make the sample transformation / color change

                    Bitmap myImage = new Bitmap(width, height, stride, PixelFormat.Format24bppRgb, newPlane0);
                    var myImage1 = ToWpfBitmap(myImage);

                    if (imageBox.Width != 0 && imageBox.Height != 0)	// Ignore when window is not visible
                    {
                        // We need to resize to the displayed area

                        imageBox.Width = imageWindowContainer.ActualWidth;
                        imageBox.Height = imageWindowContainer.ActualHeight;
                        imageBox.Source = myImage1;

                        myImage.Dispose();
                        _transform.Release(newPlane0);
                    }
                }

                _currentShownTime = bitmapData.DateTime;
                Debug.WriteLine("Image time: " + bitmapData.DateTime.ToLocalTime().ToString("HH.mm.ss.fff") + ", Mode=" + _mode);
            }
            _requestInProgress = false;
        }


        void BitmapLiveSourceLiveContentEvent(object sender, EventArgs e)
        {
            try
            {
                if (!Dispatcher.CheckAccess())
                {
                    // Make sure we execute on the UI thread before updating UI Controls
                    Dispatcher.Invoke(new EventHandler(BitmapLiveSourceLiveContentEvent), new[] { sender, e });
                }
                else
                {
                    var args = e as LiveContentEventArgs;
                    if (args != null)
                    {
                        if (args.LiveContent != null)
                        {
                            var bitmapContent = args.LiveContent as LiveSourceBitmapContent;
                            if (bitmapContent != null)
                            {
                                if (_stopLive || imageBox.Width == 0 || imageBox.Height == 0)
                                {
                                    bitmapContent.Dispose();
                                }
                                else
                                {
                                    int width = bitmapContent.GetPlaneWidth(0);
                                    int height = bitmapContent.GetPlaneHeight(0);
                                    int stride = bitmapContent.GetPlaneStride(0);
                                    IntPtr plane0 = bitmapContent.GetPlanePointer(0);

                                    IntPtr newPlane0 = _transform.Perform(plane0, stride, width, height);		// Make the sample transformation / color change
                                    Bitmap myImage = new Bitmap(width, height, stride, PixelFormat.Format24bppRgb, newPlane0);


                                    // We need to resize to the displayed area

                                    var myImageSource = ToWpfBitmap(myImage);
                                    imageBox.Width = imageWindowContainer.ActualWidth;
                                    imageBox.Height = imageWindowContainer.ActualHeight;
                                    imageBox.Source = myImageSource;
                                    myImage.Dispose();
                                    bitmapContent.Dispose();
                                    _transform.Release(newPlane0);

                                    labelDecode.Content = args.LiveContent.HardwareDecodingStatus;
                                }
                            }
                        }
                        else if (args.Exception != null)
                        {
                            // Handle any exceptions occurred inside toolkit or on the communication to the VMS

                            Bitmap bitmap = new Bitmap(320, 240);
                            Graphics g = Graphics.FromImage(bitmap);
                            g.FillRectangle(Brushes.Black, 0, 0, bitmap.Width, bitmap.Height);
                            if (args.Exception is CommunicationMIPException)
                            {
                                g.DrawString("Connection lost to server ...", new Font(System.Drawing.FontFamily.GenericMonospace, 12),
                                             Brushes.White, new PointF(20, 100));
                            }
                            else
                            {
                                g.DrawString(args.Exception.Message, new Font(System.Drawing.FontFamily.GenericMonospace, 12),
                                             Brushes.White, new PointF(20, 100));
                            }
                            g.Dispose();

                            var myImage = ToWpfBitmap(bitmap);
                            imageBox.Width = imageBox.ActualWidth;
                            imageBox.Height = imageBox.ActualHeight;
                            imageBox.Source = myImage;

                            bitmap.Dispose();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                EnvironmentManager.Instance.ExceptionDialog("BitmapLiveSourceLiveContentEvent", ex);
            }
        }


        #endregion

        #region User actions

        private void OnScrollChange(object sender, System.Windows.Controls.Primitives.ScrollEventArgs e)
        {
            _transform.SetVectors((int)(vScrollBarR.Value * hScrollBarExpose.Value*100), (int)(vScrollBarG.Value * hScrollBarExpose.Value*100), (int)(vScrollBarB.Value * hScrollBarExpose.Value*100), (int)(hScrollBarOffset.Value*100));

            if (_mode == PlaybackPlayModeData.Stop)
            {
                _nextToFetchTime = _currentShownTime;
                _redisplay = true;
            }
        }
        private void OnResize(object sender, EventArgs e)
        {
            if (_mode == PlaybackPlayModeData.Stop)
            {
                _nextToFetchTime = _currentShownTime;
                _redisplay = true;
            }
        }

        #endregion

        #region Print method

        public override void Print()
        {
            Print("Name of this item", "Some extra information");
        }

        #endregion


        #region Component events

        private void OnMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            FireDoubleClickEvent();
        }


        void ViewItemManagerPropertyChangedEvent(object sender, EventArgs e)
        {
            labelCamera.Content = _viewItemManager.SelectedCamera.Name;
            if (_selectedItem != null && _selectedItem.FQID.EqualGuids(_viewItemManager.SelectedCamera.FQID))
                return;

            ModeHandler(WindowInformation.Mode);
        }

        private void OnMouseLeftUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            FireClickEvent();
            if (_viewItemManager.SelectedCamera != null)
            {
                EnvironmentManager.Instance.SendMessage(
                    new Message(MessageId.SmartClient.SelectedCameraChangedIndication,
                                                           _viewItemManager.SelectedCamera.FQID));
            }
        }

        #endregion

        #region Component properties

        /// <summary>
        /// Gets boolean indicating whether the view item can be maximized or not. <br/>
        /// The content holder should implement the click and double click events even if it is not maximizable. 
        /// </summary>
        public override bool Maximizable
        {
            get { return true; }
        }

        /// <summary>
        /// Tell if ViewItem is selectable
        /// </summary>
        public override bool Selectable
        {
            get { return true; }
        }

        public override bool Selected
        {
            get
            {
                return base.Selected;
            }
        }
        #endregion

        private int _newWidth = 0;

        private void imageBox_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        {
            _newWidth = (int)e.NewSize.Width;
            _newHeight = (int)e.NewSize.Height;
            _setResolution = true;

            if (_bitmapLiveSource != null)
            {
                _bitmapLiveSource.Width = (int)e.NewSize.Width;
                _bitmapLiveSource.Height = (int)e.NewSize.Height;
                _bitmapLiveSource.SetWidthHeight();
            }
        }

        private int _newHeight = 0;
        private bool _setResolution = false;

    }
}
