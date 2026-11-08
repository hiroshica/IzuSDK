# AvalonDock->DirkSter99への移行関連
- ライブラリの変更

    - Nugetマネージャーで Xceed.AvalonDock 関連のライブラリを削除し、Dirkster.AvalonDock をインストールする

- XAML名前空間の宣言の変更

    - *.xamlファイルで Xceed.AvalonDock にマッピングしている宣言を Dirkster.AvalonDock へ変更する
    "http://schemas.xceed.com/wpf/xaml/avalondock"　→　"https://github.com/Dirkster99/AvalonDock"

- namespace参照の変更

    - *.csファイルで AvalonDock APIを使用する場合に usingディレクティブでnamespace参照定義している場合、都度書き換える

    using Xceed.Wpf.AvalonDock;　→　using AvalonDock;
    using Xceed.Wpf.AvalonDock.Layout;　→　using AvalonDock.Layout;
    using Xceed.Wpf.AvalonDock.Layout.Serialization;　→　using AvalonDock.Layout.Serialization;

- theme/CustomDockManager.xaml 内で Xceed.AvalonDock にマッピングしている宣言を Dirkster.AvalonDock へ変更する

    xmlns:ad="http://schemas.xceed.com/wpf/xaml/avalondock"
    xmlns:avalonDockControls="http://schemas.xceed.com/wpf/xaml/avalondock"
    xmlns:avalonDockConverters="http://schemas.xceed.com/wpf/xaml/avalondock"
    xmlns:avalonDockLayout="http://schemas.xceed.com/wpf/xaml/avalondock"
    xmlns:avalonDockProperties="clr-namespace:Xceed.Wpf.AvalonDock.Properties;assembly=Xceed.Wpf.AvalonDock"
            ↓
    xmlns:ad="https://github.com/Dirkster99/AvalonDock"
    xmlns:avalonDockControls="https://github.com/Dirkster99/AvalonDock"
    xmlns:avalonDockLayout="https://github.com/Dirkster99/AvalonDock"
    xmlns:avalonDockProperties="clr-namespace:AvalonDock.Properties;assembly=AvalonDock"
