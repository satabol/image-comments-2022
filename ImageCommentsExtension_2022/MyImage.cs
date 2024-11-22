namespace ImageCommentsExtension_2022
{
    using System.IO;
    using System.Windows;
    using Newtonsoft.Json.Linq;
    using System;
    using System.ComponentModel;
    using System.Net;
    using System.Threading;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Windows.Threading;

    /// <summary>
    /// Sub-class of Image with convenient URL-based Source changing
    /// </summary>
    internal class MyImage : Image
    {
		private double _scale;
        private VariableExpander _variableExpander;
        private FileSystemWatcher _watcher;

        public string Url { get; private set; }
        public Color BgColor { get; private set; }
        public Exception imageLoadingException = null;
        /// <summary>
        /// if exist wish to message user about some thing else set this on exception
        /// </summary>
        public string additionalExceptionInfo = "";

        public MyImage(VariableExpander variableExpander)
            : base()
        {
            if (variableExpander == null)
            {
                throw new ArgumentNullException("variableExpander");
            }
            _variableExpander = variableExpander;
        }

        /// <summary>
        /// Scale image if value is greater than 0, otherwise use source dimensions
        /// </summary>
        public double Scale
        {
            get { return _scale; }
            set
            {
                _scale = value;
                if (this.Source != null)
                {
                    // <image url="$(SolutionDir)Readme_files\file.00.png" scale="1.0"/>
                    RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
                    if (value > 0)
                    {
                        this.Width = this.Source.Width * value;
                        this.Height = this.Source.Height * value;
                    }
                    else
                    {
                        this.Width = this.Source.Width;
                        this.Height = this.Source.Height;
                    }
                }
            }
        }

        /// <summary>
        /// Sets image source and size (by scale factor)
        /// </summary>
        /// <param name="scale">If > 0, scales the image by the specified amount, otherwise uses source image dimensions</param>
        /// <param name="exception">Is set to the Exception instance if image couldn't be loaded, otherwise null</param>
        /// <returns>Returns true if image was successfully loaded, otherwise false</returns>
        public bool TrySet(string imageUrl, double scale, Color bgColor, Action refreshAction)
        {
            // Remove old watcher.
            var watcher = _watcher;
            _watcher = null;
            if(watcher!=null)
                watcher.Dispose();
            // ---
            //exception = null;
            imageLoadingException = null;
            additionalExceptionInfo = "";
            try
            {
                var expandedUrl = _variableExpander.ProcessText(imageUrl);
                if (File.Exists(expandedUrl))
                {
                    var data = new MemoryStream(File.ReadAllBytes(expandedUrl));
                    Source = BitmapFrame.Create(data);
                    // Create file system watcher to update changed image file.
                    _watcher = new FileSystemWatcher
                    {
                        //NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size,
                        Path = Path.GetDirectoryName(expandedUrl),
                        Filter = Path.GetFileName(expandedUrl)
                    };
                    var w = _watcher;
                    FileSystemEventHandler refresh = delegate
                    {
                        try
                        {
                            var enableRaisingEvents = w.EnableRaisingEvents;
                            w.EnableRaisingEvents = false;
                            if (enableRaisingEvents)
                            {
                                Url = null;
                                refreshAction();
                            }
                        }
                        catch { }
                    };
                    _watcher.Changed += refresh;
                    _watcher.Renamed += (s, a) => refresh(s, a);
                    _watcher.Deleted += refresh;
                    _watcher.EnableRaisingEvents = true;
                }
				else
				{
               		//TODO [!]: Currently, this loading system prevents images from being changed on disk, fix this
					//  e.g. using http://stackoverflow.com/questions/1763608/display-an-image-in-wpf-without-holding-the-file-open
					Uri uri = new Uri(_variableExpander.ProcessText(expandedUrl), UriKind.Absolute);

					if (uri.Scheme == "data")
						Source = BitmapFrame.Create(DataUriLoader.Load(uri));
                    else {
                        //Source = BitmapFrame.Create(uri);
                        // Не работает:
                        //Source.Changed+=delegate {
                        //    refreshAction();
                        //};

                        // Установить временное изображение:
                        { 
                            MemoryStream memoryStream = new MemoryStream();
                            Properties.Resources.ajax_loader_red_32.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Gif);
                            Source = BitmapFrame.Create(memoryStream);
                            Scale = 1.0;
                        }

                        ////Source = new BitmapImage(uri) { CacheOption = BitmapCacheOption.None };
                        BackgroundWorker bg_LoadImage = new BackgroundWorker();
                        var sc = SynchronizationContext.Current;
                        bg_LoadImage.DoWork += delegate {
                            try {
                                additionalExceptionInfo="";
                                //var stream = WebHelper.DownloadImage(uri);
                                var stream = DownloadImage(uri, out additionalExceptionInfo);
                                BitmapImage bi = new BitmapImage();
                                bi.BeginInit();
                                bi.StreamSource = (Stream)stream;
                                bi.EndInit();
                                if (Application.Current != null) {
                                    Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)delegate  // http://stackoverflow.com/questions/2329978/the-calling-thread-must-be-sta-because-many-ui-components-require-this#2329978
                                    {
                                        Source = BitmapFrame.Create(bi);
                                        this.Scale = this.Scale;
                                        refreshAction();
                                        // try animated gif in future
                                        //try { 
                                        //    GifBitmapDecoder gifBitmapDecoder = new GifBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
                                        //    Source = gifBitmapDecoder.Preview;
                                        //    this.Scale= this.Scale;
                                        //    refreshAction();
                                        //}catch(Exception _ex) {
                                        //    _ex = _ex;
                                        //}
                                    });
                                }
                            } catch (WebException _ex) {
                                try { 
                                    additionalExceptionInfo = ""; //_ex.Message;
                                    if (_ex.Response != null && _ex.Response.ContentType!=null) {
                                        additionalExceptionInfo = _ex.Response.ContentType;
                                    }
                                    if( _ex.Response.ContentType.StartsWith("application/json")==true ||
                                        _ex.Response.ContentType.StartsWith("text") == true
                                    ) {
                                        using (var stream = _ex.Response.GetResponseStream()) {
                                            using (var reader = new StreamReader(stream)) {
                                                string content = reader.ReadToEnd();
                                                additionalExceptionInfo = $". {content.Substring(0, 200)}";
                                            }
                                        }
                                    }
                                }catch(Exception _ex1) {

                                }
                                imageLoadingException = _ex;
                            } catch (Exception _ex) {
                                imageLoadingException = _ex;
                            }
                        };
                        bg_LoadImage.RunWorkerAsync();


                        // http://qaru.site/questions/3473058/converter-uri-to-bitmapimage-downloading-image-from-web
                        //var sc = SynchronizationContext.Current;
                        //new Thread(() =>
                        //{
                        //    {
                        //        var stream = WebHelper.DownloadImage(uri);

                        //        sc.Send(p =>
                        //        {
                        //            BitmapImage bi = new BitmapImage();
                        //            bi.BeginInit();
                        //            bi.StreamSource = (Stream)p;
                        //            bi.EndInit();
                        //            Source = bi;
                        //        }, stream);
                        //    }
                        //}).Start();
                    }
                }

                if (bgColor.A != 0)
                {
                    Source = ReplaceTransparency(Source, bgColor);
                    BgColor = bgColor;
                }

                Url = imageUrl;
            }
            catch (System.UriFormatException ex)
            {
                imageLoadingException = ex;
                additionalExceptionInfo = imageUrl;
                return false;
            }
            catch (Exception ex)
            {
                imageLoadingException = ex;
                return false;
            }
            this.Scale = scale;
            return true;
        }

        public override string ToString()
        {
            return Url;
        }

        private static BitmapFrame ReplaceTransparency(ImageSource bitmap, Color color)
        {
            var rect = new Rect(0, 0, (int)bitmap.Width, (int)bitmap.Height);
            var visual = new DrawingVisual();
            var context = visual.RenderOpen();
            context.DrawRectangle(new SolidColorBrush(color), null, rect);
            context.DrawImage(bitmap, rect);
            context.Close();

            var render = new RenderTargetBitmap((int)bitmap.Width, (int)bitmap.Height,
                96, 96, PixelFormats.Pbgra32);
            render.Render(visual);
            return BitmapFrame.Create(render);
        }

        public static Stream DownloadImage(Uri uri, out string additionalExceptionInfo) {
            try {
                var credentials = new NetworkCredential();
                var request = WebRequest.Create(uri);
                request.UseDefaultCredentials = true;
                WebResponse response = request.GetResponse();
                additionalExceptionInfo = response.ContentType;  // show on error with exceptions
                using (var stream = response.GetResponseStream()) {
                    Byte[] buffer = new Byte[response.ContentLength];
                    int offset = 0, actuallyRead = 0;
                    do {
                        actuallyRead = stream.Read(buffer, offset, buffer.Length - offset);
                        offset += actuallyRead;
                    } while (actuallyRead > 0);
                    return new MemoryStream(buffer);
                }
            } catch (WebException _ex) {
                throw _ex;
            } catch (Exception _ex) {
                throw _ex;
            }
        }

    }

    //public static class WebHelper {
    //    public static Stream DownloadImage(Uri uri) {
    //        try { 
    //        var credentials = new NetworkCredential();
    //        var request = WebRequest.Create(uri);
    //        request.UseDefaultCredentials = true;
    //        var response = request.GetResponse();
    //        using (var stream = response.GetResponseStream()) {
    //            Byte[] buffer = new Byte[response.ContentLength];
    //            int offset = 0, actuallyRead = 0;
    //            do {
    //                actuallyRead = stream.Read(buffer, offset, buffer.Length - offset);
    //                offset += actuallyRead;
    //            }while (actuallyRead > 0);
    //            return new MemoryStream(buffer);
    //        }
    //        }catch(WebException _ex) {

    //            throw _ex;
    //        }catch(Exception _ex) {
    //            throw _ex;
    //        }
    //    }
    //}
}
