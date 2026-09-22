# 术语表

| 术语 | 含义 |
| --- | --- |
| MTD刷写 | WiFitool 工具箱中的 mtd4 分区刷写工具。 |
| mtd4 镜像 | 要写入设备 `/dev/mtd4` 的本地固件文件。 |
| MTDWriter | 设备端 ARM ELF 刷写程序，写入 `/dev/mtd4`。 |
| MTDChecker | 设备端 ARM ELF 校验程序，校验 `/dev/mtd4` 与临时镜像。 |
