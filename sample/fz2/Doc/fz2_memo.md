## OPAOPAの動作マニュアル
- ViewPortは操作に係わらず最低0.5ドットが向いている反対に移動している

# 操作していないとき
- OPAOPAは向いている方向へ1ドット移動する
- ViewPortはOPAOPAがスクロールリミット２に到達するまで自動で２ドット移動する。

# 操作しているとき
- OPAOPAはそのまま移動。
- ViewPortはOPAOPAがスクロールリミット１の範囲内の間は停止。範囲外に出たら出た分をスクロール
----
# ToDo:
-



.\mame.exe -window -screen \\.\DISPLAY1 -debugger gdbstub -debugger_port 6868 genesis -cart C:\Users\hiro_\working\SGDK_work\MegaDriveSamples\fz2\out\rom.bin