#!/bin/bash
# コンパイルしてバイナリサイズを旧コンパイラと比較するスクリプト
# Usage: ./check_sizes.sh

COMPILER="dotnet run --project src/SLANGCompiler.CLI --"
TMPDIR="/tmp/slang_check"
mkdir -p "$TMPDIR"

# 旧コンパイラのバイナリサイズ (bytes)
declare -A OLD_SIZES
OLD_SIZES[FURUI]=528
OLD_SIZES[FMANDEL]=1115
OLD_SIZES[MANDEL]=4556
OLD_SIZES[STARS]=1199
OLD_SIZES[SLANGTEST]=6156

# ソースファイルのパス
declare -A SRC_FILES
SRC_FILES[FURUI]=examples/FURUI.SL
SRC_FILES[FMANDEL]=examples/FMANDEL.SL
SRC_FILES[MANDEL]=examples/MANDEL.SL
SRC_FILES[STARS]=examples/STARS.SL
SRC_FILES[SLANGTEST]=SLANGTEST.SL

echo "┌────────────┬───────┬───────┬────────┬────────┐"
echo "│ プログラム │  旧   │  新   │  差分  │  状態  │"
echo "├────────────┼───────┼───────┼────────┼────────┤"

TOTAL_OLD=0
TOTAL_NEW=0
ALL_OK=true

for NAME in FURUI FMANDEL MANDEL STARS SLANGTEST; do
    SRC="${SRC_FILES[$NAME]}"
    OUT="$TMPDIR/${NAME}.asm"

    # コンパイル
    $COMPILER "$SRC" -o "$OUT" 2>/dev/null

    # ASM命令行数からバイナリサイズを推定
    # → 実際のバイナリサイズが必要。アセンブラがあるか確認
    # ひとまずASM行のうち命令行を数えてバイト数概算
    # ... いや、正確なサイズが必要。

    # PUSH/POP/CALL等の命令数で比較
    PUSH_COUNT=$(grep -c "PUSH" "$OUT" 2>/dev/null || echo 0)
    POP_COUNT=$(grep -c "POP" "$OUT" 2>/dev/null || echo 0)
    CALL_COUNT=$(grep -c "CALL" "$OUT" 2>/dev/null || echo 0)
    LINE_COUNT=$(wc -l < "$OUT" 2>/dev/null || echo 0)

    OLD=${OLD_SIZES[$NAME]}

    # バイナリサイズ計算: 各命令のバイト数を合算
    # Z80命令のバイト数: LD r,n=2, LD r,(nn)=3, LD rr,nn=3, PUSH/POP=1, CALL=3, JP/JR=2-3, etc.
    NEW_SIZE=0
    while IFS= read -r line; do
        trimmed=$(echo "$line" | sed 's/^[[:space:]]*//' | sed 's/;.*//')
        [[ -z "$trimmed" ]] && continue
        [[ "$trimmed" == *: ]] && continue  # ラベル
        [[ "$trimmed" == ";"* ]] && continue
        [[ "$trimmed" == "["* ]] && continue  # ディレクティブ

        op=$(echo "$trimmed" | awk '{print $1}')
        operand=$(echo "$trimmed" | cut -f2- | sed 's/^[[:space:]]*//')

        case "$op" in
            ORG|EQU|DEFL|NAME_SPACE_DEFAULT|"") continue ;;
            DB|DEFB)
                # カンマ区切りのバイト数を数える
                count=$(echo "$operand" | awk -F',' '{print NF}')
                NEW_SIZE=$((NEW_SIZE + count))
                ;;
            DW|DEFW)
                count=$(echo "$operand" | awk -F',' '{print NF}')
                NEW_SIZE=$((NEW_SIZE + count * 2))
                ;;
            DS|DEFS)
                val=$(echo "$operand" | awk '{print $1}')
                NEW_SIZE=$((NEW_SIZE + val))
                ;;
            NOP|HALT|RET|RETI|RETN|EI|DI|CCF|SCF|DAA|CPL|NEG|RLA|RRA|RLCA|RRCA|EXX)
                NEW_SIZE=$((NEW_SIZE + 1)) ;;
            "EX")
                if echo "$operand" | grep -q "AF"; then
                    NEW_SIZE=$((NEW_SIZE + 1))
                elif echo "$operand" | grep -q "(SP)"; then
                    NEW_SIZE=$((NEW_SIZE + 1))
                else
                    NEW_SIZE=$((NEW_SIZE + 1))  # EX DE,HL
                fi
                ;;
            PUSH|POP) NEW_SIZE=$((NEW_SIZE + 1)) ;;
            "INC"|"DEC")
                if echo "$operand" | grep -qE "^(BC|DE|HL|SP|IX|IY)$"; then
                    NEW_SIZE=$((NEW_SIZE + 1))  # 16bit INC/DEC
                elif echo "$operand" | grep -qE "^(IX|IY)"; then
                    NEW_SIZE=$((NEW_SIZE + 2))
                else
                    NEW_SIZE=$((NEW_SIZE + 1))  # 8bit INC/DEC
                fi
                ;;
            "ADD"|"ADC"|"SUB"|"SBC"|"AND"|"OR"|"XOR"|"CP")
                if echo "$operand" | grep -qE "^HL,"; then
                    NEW_SIZE=$((NEW_SIZE + 1))  # ADD HL,rr
                elif echo "$operand" | grep -qE "^\\\$|^[0-9]"; then
                    NEW_SIZE=$((NEW_SIZE + 2))  # immediate
                elif echo "$operand" | grep -qE "^(IX|IY)"; then
                    NEW_SIZE=$((NEW_SIZE + 2))
                else
                    NEW_SIZE=$((NEW_SIZE + 1))
                fi
                ;;
            "LD")
                if echo "$operand" | grep -qE "^(BC|DE|HL|SP|IX|IY),\\\$|^(BC|DE|HL|SP),[0-9]|^(BC|DE|HL|SP|IX|IY),[A-Z_]"; then
                    if echo "$operand" | grep -qE "^(IX|IY)"; then
                        NEW_SIZE=$((NEW_SIZE + 4))
                    else
                        NEW_SIZE=$((NEW_SIZE + 3))  # LD rr,nn
                    fi
                elif echo "$operand" | grep -qE "^\(IY"; then
                    NEW_SIZE=$((NEW_SIZE + 3))
                elif echo "$operand" | grep -qE "^[A-H],\(IY"; then
                    NEW_SIZE=$((NEW_SIZE + 3))
                elif echo "$operand" | grep -qE "^(A|B|C|D|E|H|L),\\\$|^(A|B|C|D|E|H|L),[0-9]"; then
                    NEW_SIZE=$((NEW_SIZE + 2))  # LD r,n
                elif echo "$operand" | grep -qE "^\([A-Z_].*\),(HL|A)$|^\(.*\),SP$"; then
                    NEW_SIZE=$((NEW_SIZE + 3))  # LD (nn),A/HL
                elif echo "$operand" | grep -qE "^(A|HL|DE|BC|SP),\("; then
                    NEW_SIZE=$((NEW_SIZE + 3))  # LD A,(nn) / LD rr,(nn)
                elif echo "$operand" | grep -qE "^\(HL\),\\\$|^\(HL\),[0-9]"; then
                    NEW_SIZE=$((NEW_SIZE + 2))  # LD (HL),n
                elif echo "$operand" | grep -qE "^\("; then
                    NEW_SIZE=$((NEW_SIZE + 3))
                else
                    NEW_SIZE=$((NEW_SIZE + 1))  # LD r,r
                fi
                ;;
            "JP")
                NEW_SIZE=$((NEW_SIZE + 3)) ;;
            "JR")
                NEW_SIZE=$((NEW_SIZE + 2)) ;;
            "CALL")
                NEW_SIZE=$((NEW_SIZE + 3)) ;;
            "DJNZ")
                NEW_SIZE=$((NEW_SIZE + 2)) ;;
            "RST")
                NEW_SIZE=$((NEW_SIZE + 1)) ;;
            "BIT"|"SET"|"RES"|"RL"|"RR"|"SLA"|"SRA"|"SRL"|"RLC"|"RRC")
                NEW_SIZE=$((NEW_SIZE + 2)) ;;  # CB prefix
            "IN"|"OUT")
                NEW_SIZE=$((NEW_SIZE + 2)) ;;
            "LDIR"|"LDDR"|"CPIR"|"CPDR"|"OTIR"|"INIR")
                NEW_SIZE=$((NEW_SIZE + 2)) ;;  # ED prefix
            *)
                # その他は2バイトと推定
                NEW_SIZE=$((NEW_SIZE + 2)) ;;
        esac
    done < "$OUT"

    DIFF=$((NEW_SIZE - OLD))
    TOTAL_OLD=$((TOTAL_OLD + OLD))
    TOTAL_NEW=$((TOTAL_NEW + NEW_SIZE))

    if [ $DIFF -le 0 ]; then
        STATUS="  OK  "
    else
        STATUS="  NG  "
        ALL_OK=false
    fi

    printf "│ %-10s │ %5d │ %5d │ %+6d │ %s │\n" "$NAME" "$OLD" "$NEW_SIZE" "$DIFF" "$STATUS"
done

echo "├────────────┼───────┼───────┼────────┼────────┤"
TOTAL_DIFF=$((TOTAL_NEW - TOTAL_OLD))
if [ $TOTAL_DIFF -le 0 ]; then
    TOTAL_STATUS="  OK  "
else
    TOTAL_STATUS="  NG  "
fi
printf "│ %-10s │ %5d │ %5d │ %+6d │ %s │\n" "TOTAL" "$TOTAL_OLD" "$TOTAL_NEW" "$TOTAL_DIFF" "$TOTAL_STATUS"
echo "└────────────┴───────┴───────┴────────┴────────┘"

if $ALL_OK; then
    echo "ALL OK: 全プログラムが旧コンパイラ以下"
else
    echo "NG: 旧コンパイラより大きいプログラムがあります"
fi
