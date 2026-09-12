using System;
using System.Windows;
using System.Windows.Controls;
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
        private int _currentPageIndex = 0; // 0始まり（0 = 1ページ目）

        private bool _isFullScreen = false;
        private bool _isSingleRotateMode = false; // 1枚横向きモードのフラグ

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

        // PDF読み込み
        private void LoadPdf(string filePath)
        {
            try
            {
                _docReader?.Dispose();

                _docReader = DocLib.Instance.GetDocReader(filePath, new PageDimensions(2.0));
                _totalPages = _docReader.GetPageCount();
                _currentPageIndex = 0;

                RenderCurrentPages();
                this.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"楽譜PDFの読み込みに失敗しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // キーボード操作ハンドラ
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // 【機能1】 M キー: 上部メニューバーの表示 / 非表示切り替え
            if (e.Key == Key.M)
            {
                ToggleMenuBar();
                return;
            }

            // 【機能2】 R キー: 1枚横向き(90度回転)モードの切り替え
            if (e.Key == Key.R)
            {
                ToggleSingleRotateMode();
                return;
            }

            // F11: 全画面表示切り替え
            if (e.Key == Key.F11)
            {
                ToggleFullScreen();
                return;
            }

            if (_docReader == null) return;

            // ページ送り幅（見開きなら2、1枚モードなら1）
            int step = _isSingleRotateMode ? 1 : 2;

            // 次のページへ（右矢印、PageDown、スペース）
            if (e.Key == Key.Right || e.Key == Key.PageDown || e.Key == Key.Space)
            {
                if (_currentPageIndex + step < _totalPages)
                {
                    _currentPageIndex += step;
                    RenderCurrentPages();
                }
            }
            // 前のページへ（左矢印、PageUp）
            else if (e.Key == Key.Left || e.Key == Key.PageUp)
            {
                if (_currentPageIndex - step >= 0)
                {
                    _currentPageIndex -= step;
                    RenderCurrentPages();
                }
            }
        }

        // 【機能1の実装】メニューバーの表示/非表示トグル
        private void ToggleMenuBar()
        {
            TopMenuBar.Visibility = (TopMenuBar.Visibility == Visibility.Visible)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        // 【機能2の実装】1枚横向き（90度回転）モードの切り替え
        private void ToggleSingleRotateMode()
        {
            _isSingleRotateMode = !_isSingleRotateMode;

            if (_isSingleRotateMode)
            {
                // 1枚横向きモード:
                // 右側エリアを隠し、左側を画面全体（2列分）に広げ、90度回転させる
                RightPageBorder.Visibility = Visibility.Collapsed;
                Grid.SetColumnSpan(LeftPageBorder, 2);
                LeftImageRotation.Angle = 90; // 時計回りに90度（※反時計回りが良い場合は -90 に変更可能）
            }
            else
            {
                // 通常の見開き2枚モードに戻す:
                Grid.SetColumnSpan(LeftPageBorder, 1);
                RightPageBorder.Visibility = Visibility.Visible;
                LeftImageRotation.Angle = 0; // 回転を解除

                // 偶数始まり（見開き位置）にインデックスを補正
                if (_currentPageIndex % 2 != 0)
                {
                    _currentPageIndex -= 1;
                }
            }

            RenderCurrentPages();
        }

        // 描画処理
        private void RenderCurrentPages()
        {
            if (_docReader == null || _totalPages == 0) return;

            if (_isSingleRotateMode)
            {
                // --- 1枚横向き表示 ---
                LeftPageImage.Source = RenderPageToBitmap(_currentPageIndex);
                PageInfoText.Text = $"[1枚横向] {_currentPageIndex + 1} / {_totalPages} ページ";
            }
            else
            {
                // --- 見開き2枚表示 ---
                LeftPageImage.Source = RenderPageToBitmap(_currentPageIndex);

                int rightPageIndex = _currentPageIndex + 1;
                if (rightPageIndex < _totalPages)
                {
                    RightPageImage.Source = RenderPageToBitmap(rightPageIndex);
                    PageInfoText.Text = $"{_currentPageIndex + 1}-{rightPageIndex + 1} / {_totalPages} ページ";
                }
                else
                {
                    // 最終ページが奇数の場合は右側を空白にする
                    RightPageImage.Source = null;
                    PageInfoText.Text = $"{_currentPageIndex + 1} / {_totalPages} ページ (最終ページ)";
                }
            }
        }

        // PDFの指定ページをBitmap画像に変換
        private BitmapSource RenderPageToBitmap(int pageIndex)
        {
            using (var pageReader = _docReader.GetPageReader(pageIndex))
            {
                var width = pageReader.GetPageWidth();
                var height = pageReader.GetPageHeight();
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

                bitmap.Freeze();
                return bitmap;
            }
        }

        // 全画面表示のトグル
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

        protected override void OnClosed(EventArgs e)
        {
            _docReader?.Dispose();
            base.OnClosed(e);
        }
    }
}