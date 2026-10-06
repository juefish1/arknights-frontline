# 开发规则

- 所有开发工作都在 `mouse` 分支进行。
- 每次开发前，先处理本地未提交的改动，再切换到 `mouse`，获取远程最新代码，并将远程 `main` 合并到 `mouse`：

  ```sh
  git switch mouse
  git fetch origin
  git merge origin/main
  ```

- 如果合并出现冲突，先解决冲突并完成合并，再继续开发。
- 开发完成并通过必要验证后，在 `mouse` 上提交改动，然后推送到远程 `mouse`：

  ```sh
  git add <本次修改的文件>
  git commit -m "说明本次修改"
  git push origin mouse
  ```

- 不直接向 `main` 提交或推送开发改动。

以上命令假定 `origin` 指向 `https://github.com/nie-yao/arknights-frontline.git`。
