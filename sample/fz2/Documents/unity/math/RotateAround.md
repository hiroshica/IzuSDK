## 要求・ワールドの任意点で物体を回したい
- Unityならば**transform**の**RotateAround**で実現できるが原理がわからないと後で困るので計算式を解説しておく。

# 実際の操作
物体Aを任意点Ｐの位置でY軸で45°回転させたい

# 計算概念
点Pを原点Oに移動させて45°回して移動させた分戻す。
- 点Pを原点に移動させる計算は(O-P)の移動マトリックスを作る(M1)
- 上記の移動をもとに戻すためのマトリックスを作る。(M1D)
- 回転させるには回転マトリックスを作る(M2)
- **transform**からマトリックスを取り出す(M0)
- 式　MA = (M1D * (M2 * (M1 * M0)))　を解く
- MAから移動値、回転値を取り出して*transform*に入れる 
```
            Vecvtor3 O = Vector3.zero;
            Vector3 P = new Vector3(10,0,10);;
            float rotateAngle = 45.0f;
            Vector3 rotateAxis = new Vector3(0, 1, 0);

            var GetVector = O-P;
            var M1 = new Matrix4x4();
            var M1D = new Matrix4x4();
            var M2 = new Matrix4x4();
            M1.SetTRS(GetVector, quaternion.identity, Vector3.one);
            M1D.SetTRS(-GetVector, quaternion.identity, Vector3.one);

            var rotate = Quaternion.AngleAxis(rotateAngle, rotateAxis);
            M2.SetTRS(Vector3.zero, rotate, Vector3.one);

            var M0 = transform.localToWorldMatrix;

            var MA = (M1D * (M2 * (M1 * M0)))
            // 移動成分をとりだす
            transform.position = MA.GetPosition();
            // 回転成分をとりだす
            transform.rotation = MA.rotation;
```
