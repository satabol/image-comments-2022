using System;
using Microsoft.VisualStudio.Language.Intellisense;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Net;
using System.Windows.Media.Imaging;
using System.IO;
using System.Windows;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Media;
using System.Windows.Interop;
using System.ComponentModel;
using System.Windows.Media.Animation;
using System.Threading;
using System.Runtime.InteropServices;

namespace ImageCommentsExtension_2022 {

    // http://w3ka.blogspot.ru/2009/12/how-to-fix-webclient-timeout-issue.html
    class WebDownload : WebClient {
        /// <summary>
        /// Time in milliseconds
        /// </summary>
        public int Timeout { get; set; }

        public WebDownload() : this(60000) { }

        public WebDownload( int timeout ) {
            this.Timeout = timeout;
        }

        protected override WebRequest GetWebRequest( Uri address ) {
            var request = base.GetWebRequest(address);
            if (request != null) {
                request.Timeout = this.Timeout;
            }
            return request;
        }
    }
    
    // Наличие интерфейса IInteractiveQuickInfoContent в этом объекте не очевидно никак!
    // В документации (https://msdn.microsoft.com/en-us/library/microsoft.visualstudio.language.intellisense.iinteractivequickinfocontent.aspx)
    // написано: "If any object implementing this interface is provided by IQuickInfoSource via AugmentQuickInfoSession, 
    // the Quick Info presenter will allow it to interact with IQuickInfoSession for this content." что означает, что 
    // в функции "AugmentQuickInfoSession" (см. ниже) необходимо для добавления в список во второй аргумент использовать объект,
    // который реализует интерфейс "IInteractiveQuickInfoContent", но в примере "Displaying QuickInfo Tooltips: https://msdn.microsoft.com/en-us/library/ee197646.aspx"
    // в качестве объекта для добавления используется тип "String" (строка "qiContent.Add(value);"), на основе String в принципе нельзя сделать новый класс, которому можно добавить и
    // реализовать этот интерфейс. Странно. Однако в качестве элемента для списка "IList<object> qiContent" указан тип "object". Это несколько "радует",
    // однако неясно, что же можно указать в качестве такого элемента? Ранее я читал, что в качестве компонентной графической системы для расширения VSIX
    // используется WPF. Поэтому и рискнул предположить, что графические элементы WPF являются расширяемым, что и было реализовано в этом классе.
    class HREFPreview : UserControl, IInteractiveQuickInfoContent {
        public bool KeepQuickInfoOpen {
            get {
                //throw new NotImplementedException();
                // https://docs.microsoft.com/ru-ru/dotnet/api/microsoft.visualstudio.language.intellisense.iinteractivequickinfocontent.keepquickinfoopen?view=visualstudiosdk-2017
                return true; // true - do not close QuickInfo after click.
            }
        }

        public bool IsMouseOverAggregated {
            get {
                //throw new NotImplementedException();
                return true;
            }
        }

        [DllImport("urlmon.dll", CharSet = CharSet.Ansi)]
        private static extern int UrlMkSetSessionOption( int dwOption, string pBuffer, int dwBufferLength, int dwReserved );
        const int URLMON_OPTION_USERAGENT = 0x10000001;
        const int URLMON_OPTION_USERAGENT_REFRESH = 0x10000002;
        public void ChangeUserAgent() {

            string ua = "Mozilla/5.0 (Windows NT 6.3; WOW64; Trident/7.0; rv:11.0) like Gecko";
            UrlMkSetSessionOption(URLMON_OPTION_USERAGENT, ua, ua.Length, 0);
            UrlMkSetSessionOption(URLMON_OPTION_USERAGENT_REFRESH, ua, ua.Length, 0);

        }

        // Элемент, в который выводится результат запроса.
        private UIElement wpf_result = null;
        private Grid grid = null; // Сетка для отображения элементов управления и предпросмотра изображения.
        public HREFPreview( string url ) {
            ChangeUserAgent();
            // Так определять Grid гораздо проще: http://stackoverflow.com/questions/5755455/how-to-set-control-template-in-code
            string str_template = @"
                            <ControlTemplate 
                                                xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                                                xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                                                xmlns:local='clr-namespace:TestQuickInfo'
                             >
                                <Grid x:Name='grid'>
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width='*'/>
                                    </Grid.ColumnDefinitions>
                                    <Grid.RowDefinitions>
                                        <RowDefinition Height='*'/>
                                        <RowDefinition Height='*'/>
                                    </Grid.RowDefinitions>
                                </Grid>
                            </ControlTemplate>";
            ControlTemplate ct = (ControlTemplate)XamlReader.Parse(str_template);
            this.Template = ct;
            if (this.ApplyTemplate()) {
                grid = (Grid)ct.FindName("grid", this);

                {
                    //Button goto_button = new Button() {
                    //    Content = "Open url in browser: " + url
                    //};
                    //goto_button.Click+= ( e, a ) => {
                    //    Process.Start(url, "");
                    //};
                    //Grid.SetColumn(goto_button, 0);
                    //Grid.SetRow(goto_button, 0);
                    //grid.Children.Add(goto_button);
                }
                {
                    // С картинкой меню выглядит симпатичнее, чем просто кнопка, но можно переделать.
                    MenuItem mi_goto = new MenuItem() {
                        Name = "mi_goto",
                    };

                    // Совершенно, невероятно, непредсказуемый способ читать иконку из ресурса. Блять! Всякие другие загрузки из URI НЕ РАБОТАЛИ!!! Убить на эту ХРЕНЬ 4 ЧАСА!!! ОХРЕНЕТЬ!!!
                    // http://stackoverflow.com/questions/1127647/convert-system-drawing-icon-to-system-media-imagesource?answertab=votes#tab-top
                    Bitmap bitmap = Properties.Resources.application_x_mswinurl.ToBitmap();
                    IntPtr hBitmap = bitmap.GetHbitmap();

                    ImageSource imageSource = Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    mi_goto.Icon = new System.Windows.Controls.Image { Source = imageSource };
                    mi_goto.Header = $"Open '{url}'";

                    mi_goto.Click += ( e, a ) => {
                        Process.Start(url, "");
                    };
                    Grid.SetColumn(mi_goto, 0);
                    Grid.SetRow(mi_goto, 0);
                    grid.Children.Add(mi_goto);
                }
                {
                    // С картинкой меню выглядит симпатичнее, чем просто кнопка, но можно переделать.
                    MenuItem mi_clipboard = new MenuItem() {
                        Name = "mi_clipboard",
                    };

                    // Совершенно, невероятно, непредсказуемый способ читать иконку из ресурса. Блять! Всякие другие загрузки из URI НЕ РАБОТАЛИ!!! Убить на эту ХРЕНЬ 4 ЧАСА!!! ОХРЕНЕТЬ!!!
                    // http://stackoverflow.com/questions/1127647/convert-system-drawing-icon-to-system-media-imagesource?answertab=votes#tab-top
                    Bitmap bitmap = Properties.Resources.clipboard.ToBitmap();
                    IntPtr hBitmap = bitmap.GetHbitmap();

                    ImageSource imageSource = Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    mi_clipboard.Icon = new System.Windows.Controls.Image { Source = imageSource };
                    mi_clipboard.Header = $"Clipboard '{url}'";

                    mi_clipboard.Click += ( e, a ) => {
                        try { 
                            Clipboard.SetDataObject(url);
                        }catch(Exception _ex) {

                        }
                    };
                    Grid.SetColumn(mi_clipboard, 0);
                    Grid.SetRow(mi_clipboard, 1);
                    grid.Children.Add(mi_clipboard);
                }
            }
        }

        private void replace_wpf_result(UIElement new_wpf_result ) {
            if(new_wpf_result == null) {
                return;
            }
            if (wpf_result != null) {
                grid.Children.Remove(wpf_result);
            }
            wpf_result = new_wpf_result;
            Grid.SetColumn(wpf_result, 0);
            Grid.SetRow(wpf_result, 1);
            grid.Children.Add(wpf_result);
        }

        // Снять скриншот с WPF- элемента.
        // см. http://stephenmeehan.com/2008/02/wpf-c-website-thumbnail/ 
        // https://blogs.msdn.microsoft.com/llobo/2007/02/26/capturing-frame-content/
        private DrawingImage GenerateThumbnail( WebBrowser webBrowser ) {
            DrawingImage drawingImage = null;
            for (Visual v = webBrowser; v != null; v = VisualTreeHelper.GetChild(v, 0) as Visual) {

                if (v.ToString().ToLower().Contains("webbrowser")) {

                    drawingImage = new DrawingImage(VisualTreeHelper.GetDrawing(v));
                    break;
                }
            }
            return drawingImage;
        }
    }
}
