# Unity 项目合作流程

## 仓库与分支

- 在自己的 Fork 仓库的 `mouse` 分支开发。
- `origin` 指向自己的 Fork：`https://github.com/juefish1/arknights-frontline.git`。
- `upstream` 指向作者仓库：`https://github.com/nie-yao/arknights-frontline.git`。
- 首次参与时 Fork 作者仓库并克隆自己的 Fork，然后创建或切换到 `mouse` 分支。

## 每次开发前

- 先妥善处理本地未提交的改动，再同步作者的最新 `main`，保持代码最新并降低合并冲突的可能：

  ```sh
  git switch mouse
  git fetch upstream
  git merge upstream/main
  ```

- 如果合并出现冲突，先解决冲突并完成合并，再继续开发。
- 用 Unity Hub 打开项目，使用 `ProjectSettings/ProjectVersion.txt` 指定的 Unity 版本；当前为 `6000.3.25f1`，以该文件为准。

## 提交与推送

- 提交 Unity 资源时，同时提交对应的 `.meta` 文件；资源移动或删除时同步处理对应的 `.meta` 文件。
- 不提交 `Library`、`Temp`、`Logs` 等生成目录，并遵循项目 `.gitignore`。
- 修改完成并进行必要验证后，只提交本次相关改动，并推送到自己的 Fork 的 `mouse`：

  ```sh
  git add <本次修改的文件及对应的 .meta 文件>
  git commit -m "说明本次修改"
  git push -u origin mouse
  ```

## Pull Request

- 在 GitHub 创建 PR：目标（base）为 `nie-yao/arknights-frontline` 的 `main`；来源（head）为 `juefish1/arknights-frontline` 的 `mouse`。
- 在 PR 中简要说明修改内容及验证情况，未执行的验证如实注明。
- 由作者审阅批准后合并，不直接推送作者仓库的开发分支或 `main`。
- 如需调整，继续提交并推送到自己 Fork 的 `mouse`，现有 PR 会自动更新。
