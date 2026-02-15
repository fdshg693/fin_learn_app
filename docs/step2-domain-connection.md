# STEP2: Domainの接続（既存モデルを参照）

## 目的
既存のDomainモデルをバックエンドMVCから参照できるようにする。

## 実施内容
1. Domainクラスライブラリを作成
   - src/Domain/FinLearnApp.Domain.csproj を作成。
2. ソリューションに追加
   - FinLearnApp.sln に Domain プロジェクトを追加。
3. MVCバックエンドから参照
   - backend/FinLearnApp.Api が Domain を参照するように設定。

## 生成/更新された主な構成
- src/Domain/FinLearnApp.Domain.csproj
- FinLearnApp.sln（Domain追加）
- backend/FinLearnApp.Api/FinLearnApp.Api.csproj（参照追加）

## 補足
- Domain配下の既存モデル（Entities / ValueObjects / Enums / Events）はそのまま使用。
