

VAR VAL1,VAL2,VAL3;

MACHINE MULFIX(2):_MULFIX;

MAIN()
{
	VAL1 = 8 * 256;
	VAL2 = 16 * 256;
	VAL3 = MULFIX(VAL1,VAL2);

	PRINT(VAL3);
}




#ASM
_MULFIX:
; 入力:
; HL = 固定小数点数1 (整数部8ビット, 小数部8ビット)
; DE = 固定小数点数2 (整数部8ビット, 小数部8ビット)
;
; 出力:
; HL = 結果 (整数部8ビット, 小数部8ビット)
;multiply_fixed_point:
    ; (HL * DE)の整数部を計算
    ld A, H          ; A = HLの整数部
    ld B, D          ; B = DEの整数部
    call multiply_8bit
    ld B, A          ; 結果をBに保存

    ; (HL * DE)の小数部を計算
    ld A, L          ; A = HLの小数部
    ld B, E          ; C = DEの小数部
    call multiply_8bit
    rrca             ; 結果を右シフトして、8ビット精度を維持
    rrca
    rrca
    rrca
    rrca
    rrca
    rrca
    rrca
    ld H, A          ; 結果をHに保存

    ; B (整数部の結果) と H (小数部の結果) を合成
    ld L, H          ; 小数部をLに保存
    pop B
    ld H, B          ; 整数部をHに保存
    ret

; A = 8ビットの整数1
; B = 8ビットの整数2
;
; 出力:
; A = A * Bの下位8ビット
multiply_8bit:
    ld C, B          ; Bレジスタの値をCに保存
    ld B, 0          ; Bレジスタをゼロで初期化 (積の下位8ビット)
    ld HL, 0         ; HLレジスタペアをゼロで初期化 (ループカウンタ)

multiply_8bit_loop:
    bit 0, C         ; Cの最下位ビットが1かどうかチェック
    jr z, no_add     ; 最下位ビットが0の場合、加算スキップ
    add A, B         ; A += B (下位8ビットの加算)
    jr nc, no_add    ; キャリーがない場合、HLに加算しない
    inc H            ; キャリーがある場合、HLの下位バイトに1を加算

no_add:
    sla C            ; Cを左にシフト (次のビットをチェック)
    rl B             ; Bを左にシフト (積の下位8ビットを更新)
    rl H             ; Hを左にシフト (積の上位8ビットを更新)
    djnz multiply_8bit_loop ; HLのループカウンタをデクリメントし、0でない場合はループを続ける

    ret              ; 結果 (下位8ビット) が A レジスタに格納されている

#END
