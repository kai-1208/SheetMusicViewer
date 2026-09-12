using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Docnet.Core;
using Docnet.Core.Converters;
using Docnet.Core.Models;
using Docnet.Core.Readers;

namespace SheetMusicViewer
{
    public partial class MainWindow : Window
    {
        private IDocReader _docReader;
        private int _totalPages = 0;
        private int _currentLeftPageIndex = 0; // 0始まり（0 = 1ページ目）
        private bool _isFullScreen = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        // [楽譜(PDF)を開く] ボタン押下時
        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "PDF楽譜ファイル (*.pdf)|*.pdf|すべてのファイル (*.*)|*.*",
                Title = "楽譜PDFを選択"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                LoadPdf(openFileDialog.FileName);
            }
        }

        // PDFの読み込み
        private void LoadPdf(string filePath)
        {
            try
            {
                _docReader?.Dispose();

                // 楽譜をノートPC画面でくっきり読めるよう高解像度(2.0倍)でレンダリング
                _docReader = DocLib.Instance.GetDocReader(filePath, new PageDimensions(2.0));
                _totalPages = _docReader.GetPageCount();
                _currentLeftPageIndex = 0;

                RenderCurrentPages();
                this.Focus(); // キー操作をすぐ受け取れるようにフォーカスを当てる
            }
            catch (Exception ex)
            {
                MessageBox.Show($"楽譜PDFの読み込みに失敗しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // キーボード操作（ページ送り/戻し、全画面）
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // F11キーで全画面表示の切り替え（演奏時に便利）
            if (e.Key == Key.F11)
            {
                ToggleFullScreen();
                return;
            }

            if (_docReader == null) return;

            // 次のページへ（右矢印、PageDown、スペースキー）
            if (e.Key == Key.Right || e.Key == Key.PageDown || e.Key == Key.Space)
            {
                // 次のページが存在すれば2ページ進める
                if (_currentLeftPageIndex + 2 < _totalPages)
                {
                    _currentLeftPageIndex += 2;
                    RenderCurrentPages();
                }
            }
            // 前のページへ（左矢印、PageUp）
            else if (e.Key == Key.Left || e.Key == Key.PageUp)
            {
                // 前のページが存在すれば2ページ戻す
                if (_currentLeftPageIndex - 2 >= 0)
                {
                    _currentLeftPageIndex -= 2;
                    RenderCurrentPages();
                }
            }
        }

        // 左右のページを描画
        private void RenderCurrentPages()
        {
            if (_docReader == null || _totalPages == 0) return;

            // 1. 左ページの描画（常に存在するページ）
            LeftPageImage.Source = RenderPageToBitmap(_currentLeftPageIndex);

            // 2. 右ページの描画判定
            int rightPageIndex = _currentLeftPageIndex + 1;

            if (rightPageIndex < _totalPages)
            {
                // 右ページが存在する場合（偶数ページ数がある通常時）
                RightPageImage.Source = RenderPageToBitmap(rightPageIndex);
                PageInfoText.Text = $"{_currentLeftPageIndex + 1}-{rightPageIndex + 1} / {_totalPages} ページ";
            }
            else
            {
                // 最終ページが奇数の場合、右側は空白にして1枚のみ表示
                RightPageImage.Source = null;
                PageInfoText.Text = $"{_currentLeftPageIndex + 1} / {_totalPages} ページ (最終ページ)";
            }
        }

        // PDFの指定ページをWPF用のBitmap画像に変換
        private BitmapSource RenderPageToBitmap(int pageIndex)
        {
            using (var pageReader = _docReader.GetPageReader(pageIndex))
            {
                var width = pageReader.GetPageWidth();
                var height = pageReader.GetPageHeight();

                // 楽譜の透過によるチラつきを防ぐため、白背景(RGB: 255, 255, 255)でレンダリング
                var rawBytes = pageReader.GetImage(new NaiveTransparencyRemover(255, 255, 255));

                var bitmap = BitmapSource.Create(
                    width,
                    height,
                    96,
                    96,
                    PixelFormats.Bgra32,
                    null,
                    rawBytes,
                    width * 4
                );

                bitmap.Freeze(); // 描画処理の高速化
                return bitmap;
            }
        }

        // 全画面表示のトグル切り替え
        private void ToggleFullScreen()
        {
            if (!_isFullScreen)
            {
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Maximized;
                _isFullScreen = true;
            }
            else
            {
                WindowStyle = WindowStyle.SingleBorderWindow;
                WindowState = WindowState.Normal;
                _isFullScreen = false;
            }
        }

        // アプリ終了時にPDFリソースを確実に解放
        protected override void OnClosed(EventArgs e)
        {
            _docReader?.Dispose();
            base.OnClosed(e);
        }
    }
}