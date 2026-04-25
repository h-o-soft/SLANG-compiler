; PR-B 二段アセンブル E2E テスト用の最小 runtime library。
;
; SemanticAnalyzer の builtin 関数 BEEP に対して runtime 実装を提供し、
; @resident shared を付与することで、`#MODULE RESIDENT` × shared 関数の
; main 集約が二段アセンブル後に実バイナリで動くことを検証する。
;
; 中身は `RET` 1 バイトだけ。テストはアドレス解決 (overlay の CALL が main
; 内 BEEP アドレスを指す) を確認するだけなので、実際の挙動は問わない。

; @name BEEP
; @param_count 0
; @resident shared
RET
