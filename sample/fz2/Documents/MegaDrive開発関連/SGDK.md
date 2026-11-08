# SGDK使い方メモ

https://github.com/Stephane-D/SGDK/releases
#### このドキュメント作成時バージョン　”SGDK 2.00 (january 2024)”
 
 
基本的にはサンプルを参考にいろいろ作成するのがいいと思うが、ちょっとでもハードでできそうかな？ってことにチャレンジすると息詰まるので
それらをまとめておく。

## SGDKの基本仕様
- 68kメモリ周りは基本グローバルに宣言した変数を使うことになる
- 上記方法で確保したメモリ分とスタック用メモリサイズを64KByteから引いた残ったメモリを"MEM_alloc"関連で確保できると思う。
- 














## VRAMに関すること
- SGDKはTile Data(日本ではCellData or キャラジェネ) を　BG/SPR共通として $0000アドレスからとしています。
- これらを便利に計算するためのシステムが用意されていて "VRAM_???"関数群です。使い方は以下のサイトを参照してください。

https://www.gamopat-forum.com/t97696p180-sgdk-sega-megadrive-genesis-development-kit





## SPR_???を便利に使えるようにするためにテストしたこと



