using System.IO;
using Microsoft.VisualStudio.Language.Intellisense;
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
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Interop;
using System.Windows.Input;

namespace ImageCommentsExtension_2022 {
    /// <summary>
    /// Interaction logic for MyImageControl.xaml
    /// </summary>
    public partial class MyImageControl : UserControl, IInteractiveQuickInfoContent {

        static MyImageControl() {
            DependencyAssemblyLoader.Main();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void RaisePropertyChanged(string propertyName) {
            // Если кто-то на него подписан, то вызывем его
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public MyImageControl() {
            InitializeComponent();
            Header = " ImageComments v." + FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).FileVersion;
            DataContext = this;
        }

        private string _Url;

        public string Url {
            get { return _Url; }
            set { _Url = value; 
                RaisePropertyChanged(nameof(Url));
            }
        }


        public MyImageControl(string fileName) {
            InitializeComponent();

            Header = " ImageComments v." + FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).FileVersion;

            DataContext = this;
            SetUrl(fileName);
        }

        public void SetUrl(string fileName) {
            Url = fileName;

            // Очень странно, но выставлять иконки приходится вручную:
            {
                {
                    Bitmap bitmap = Properties.Resources.application_x_mswinurl_16x16;
                    IntPtr hBitmap = bitmap.GetHbitmap();
                    ImageSource imageSource = Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    mi_OpenUrl.Icon = new System.Windows.Controls.Image { Source = imageSource };
                }
                {
                    Bitmap bitmap = Properties.Resources.clipboard_128x128;
                    IntPtr hBitmap = bitmap.GetHbitmap();
                    ImageSource imageSource = Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    mi_ClipboardUrl.Icon = new System.Windows.Controls.Image { Source = imageSource };
                }
                {
                    Bitmap bitmap = Properties.Resources.folder;
                    IntPtr hBitmap = bitmap.GetHbitmap();
                    ImageSource imageSource = Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    mi_OpenParentFolder.Icon = new System.Windows.Controls.Image { Source = imageSource };
                }
                {
                    Bitmap bitmap = Properties.Resources.external_window;
                    IntPtr hBitmap = bitmap.GetHbitmap();
                    ImageSource imageSource = Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    btn_OpenExternalWindow.Content = new System.Windows.Controls.Image { Source = imageSource };
                }
            }

            tb_PANZOOM_Control.Visibility = Visibility.Collapsed;
            control_Viewport.Visibility = Visibility.Collapsed;

            if (ImageUrlQuickInfoSource.httpUrlRegex.IsMatch(Url) == true) {
                {
                    MemoryStream memoryStream = new MemoryStream();
                    Properties.Resources.ajax_loader_red_32.Save(memoryStream, ImageFormat.Gif);
                    XamlAnimatedGif.AnimationBehavior.SetSourceStream(aImage, memoryStream);
                    tb_PANZOOM_Control.Visibility = Visibility.Visible;
                    control_Viewport.Visibility = Visibility.Visible;

                }

                // Загрузить изображение по URL
                BackgroundWorker bg_LoadImage = new BackgroundWorker();
                bg_LoadImage.DoWork += delegate {
                    string mime = "";
                    Stream imageStream = null;
                    try {
                        imageStream = MyImage.DownloadImage(new Uri(Url), out mime);
                        //if (Application.Current != null) 
                        {
                            Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)delegate  // http://stackoverflow.com/questions/2329978/the-calling-thread-must-be-sta-because-many-ui-components-require-this#2329978
                            {
                                try {
                                    XamlAnimatedGif.AnimationBehavior.SetSourceStream(aImage, imageStream);
                                    if (aImage.Source != null) {
                                        tb_PANZOOM_Control.Visibility = Visibility.Visible;
                                        control_Viewport.Visibility = Visibility.Visible;
                                    } else {
                                        throw new Exception($"Unknown format image file.");
                                    }
                                } catch (Exception _ex) {
                                    tb_PANZOOM_Control.Visibility = Visibility.Hidden;
                                    control_Viewport.Visibility = Visibility.Hidden;
                                    tb_Comment.Text = $"Can't show mime type as image: {(mime == null ? "undefined" : mime)}:\n{_ex.ToString()}";
                                }
                            });
                        }
                    } catch (WebException _ex) {
                        try {
                            if (_ex.Response != null && _ex.Response.ContentType != null) {
                                mime = _ex.Response.ContentType;
                            }
                            if (mime.StartsWith("application/json") == true ||
                                mime.StartsWith("text") == true
                            ) {
                                using (var stream = _ex.Response.GetResponseStream()) {
                                    using (var reader = new StreamReader(stream)) {
                                        string content = reader.ReadToEnd();
                                        Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)delegate  // http://stackoverflow.com/questions/2329978/the-calling-thread-must-be-sta-because-many-ui-components-require-this#2329978
                                        {
                                            tb_PANZOOM_Control.Visibility = Visibility.Collapsed;
                                            control_Viewport.Visibility = Visibility.Collapsed;

                                            if (content.Length > 400) {
                                                tb_Comment.Text = $". {content.Substring(0, 400)}";
                                            } else {
                                                tb_Comment.Text = $". {content}";
                                            }
                                        });
                                    }
                                }
                            }
                        } catch (Exception _ex1) {
                            Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)delegate  // http://stackoverflow.com/questions/2329978/the-calling-thread-must-be-sta-because-many-ui-components-require-this#2329978
                            {
                                tb_Comment.Text = _ex1.ToString();
                            });
                        }
                    } catch (Exception _ex) {
                        Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)delegate  // http://stackoverflow.com/questions/2329978/the-calling-thread-must-be-sta-because-many-ui-components-require-this#2329978
                        {
                            tb_Comment.Text = _ex.ToString();
                        });
                    }
                };
                bg_LoadImage.RunWorkerAsync();
            } else if (File.Exists(fileName) == true) {
                byte[] ba = File.ReadAllBytes(fileName);
                Stream imageStream = new MemoryStream(ba);
                // Подходит любой формат!!! WTF!!!
                try {
                    XamlAnimatedGif.AnimationBehavior.SetSourceStream(aImage, imageStream);
                    tb_PANZOOM_Control.Visibility = Visibility.Visible;
                    control_Viewport.Visibility = Visibility.Visible;
                } catch (Exception _ex) {
                    tb_Comment.Text = $"Не удалось вывести '{fileName}' как изображение:\n{_ex.ToString()}";
                }
            } else if (File.Exists(fileName) == false) {
                tb_Comment.Text = $"File name '{fileName}' not found.";
            }
        }

        private VariableExpander _variableExpander;

        public MyImageControl(VariableExpander variableExpander)
            : base() {
            if (variableExpander == null) {
                throw new ArgumentNullException("variableExpander");
            }
            DataContext = this;
            _variableExpander = variableExpander;
        }

        //public ImageSource Source;

        public bool KeepQuickInfoOpen {
            get {
                //throw new NotImplementedException();
                // https://docs.microsoft.com/ru-ru/dotnet/api/microsoft.visualstudio.language.intellisense.iinteractivequickinfocontent.keepquickinfoopen?view=visualstudiosdk-2017
                return true; // true - do not close QuickInfo after click.
            }
        }

        public bool IsMouseOverAggregated {
            get {
                return true;
            }
        }

        private void mi_OpenUrl_Click(object sender, RoutedEventArgs e) {
            Process.Start(Url, "");
        }

        private void mi_ClipboardUrl_Click(object sender, RoutedEventArgs e) {
            try {
                Clipboard.SetDataObject(Url);
            }catch(Exception _ex) {

            }
        }

        private void imageForPost_KeyDown(object sender, KeyEventArgs e) {
                //if (e.Key == Key.F) {
                //    zoomBorder.Fill();
                //}

                //if (e.Key == Key.R) {
                //    zoomBorder.Reset();
                //}
        }

        private void mi_OpenParentFolderUrl_Click(object sender, RoutedEventArgs e) {
            try {

                string fileName = Path.GetFullPath(Url);
                if (File.Exists(fileName) == true) {
                    Process.Start("explorer.exe", $"/select,\"{fileName}\"");
                } else { 
                    string parent_folder = fileName;
                    parent_folder = Path.GetDirectoryName(parent_folder);
                    while (Directory.Exists(parent_folder)==false && parent_folder.Length>0) {
                        parent_folder = Path.GetDirectoryName(parent_folder);
                    }
                    if(parent_folder.Length > 0) { 
                        Process.Start("explorer.exe", $"{parent_folder}");
                    }
                }
            }catch(Exception _ex) {
                
            }
        }

        private string _Header = "ImageComments plugin";

        public string Header {
            get { return _Header; }
            set { 
                _Header = value;
                RaisePropertyChanged(nameof(Header));
            }
        }

        private void mi_ExternalWindow_Click(object sender, RoutedEventArgs e) {
            if (Url != null && Url.Length>0) { 
                ExternalWindow ew = new ExternalWindow(Url);
                ew.ShowInTaskbar = false;
                ew.ShowActivated = true;
                ew.Show();
            }
        }
    }
}
