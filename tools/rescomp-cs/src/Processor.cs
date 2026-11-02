namespace Sgdk.Rescomp;

/// <summary>
/// .res の 1 行 (リソースの種類ごと) を処理するもの。Java 版の sgdk.rescomp.Processor にあたる。
/// 拡張は、この interface を実装した public なクラス (引数なしのコンストラクタ付き) を *_ext.dll に入れて、
/// .res と同じフォルダに置く。
/// </summary>
public interface IProcessor
{
    /// <summary>リソースの種類の名前 (.res の行の最初の語。大文字小文字は区別しない)。</summary>
    string Id { get; }

    /// <summary>
    /// 行を処理してリソースを返す。fields[0] は種類の名前で、引用符は外してある。
    /// 書き方が違うときは使い方を表示して null を返す。
    /// </summary>
    Resource Execute(string[] fields);
}
