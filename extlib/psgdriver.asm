; ====================================================================================================
;
; PSG Sound Driver
;
; licence:MIT Licence
; copyright-holders:Hitoshi Iwai(aburi6800)
;
; ====================================================================================================

; SECTION code_user
; 
; PUBLIC SOUNDDRV_INIT
; PUBLIC SOUNDDRV_EXEC
; PUBLIC SOUNDDRV_BGMPLAY
; PUBLIC SOUNDDRV_SFXPLAY
; PUBLIC SOUNDDRV_STOP
; PUBLIC SOUNDDRV_PAUSE
; PUBLIC SOUNDDRV_RESUME
; PUBLIC SOUNDDRV_STATE

#LIB PSG_INIT
; ====================================================================================================
; DRIVER INITIALIZE
; ====================================================================================================
SOUNDDRV_INIT:
    DI
    PUSH AF
    PUSH BC
    PUSH DE
    PUSH HL
    PUSH IX
    PUSH IY

	CALL GICINI		                ; GICINI	PSGの初期化

    ; ■H.TIMIバックアップ
    LD HL,H_TIMI                    ; 転送元
    LD DE,SOUNDDRV_H_TIMI_BACKUP    ; 転送先
    LD BC,5                         ; 転送バイト数
    LDIR

    ; ■H.TIMI書き換え
    LD A,$C3                        ; JP
    LD HL,SOUNDDRV_EXEC             ; サウンドドライバのアドレス
    LD (H_TIMI+0),A
    LD (H_TIMI+1),HL

    ; ■音を出す設定
	LD A,7			                ; PSGレジスタ番号=7(チャンネル設定)
	LD E,%10111111	                ; 各チャンネルのON/OFF設定 0:ON 1:OFF,10+NOISE C～A+TONE C～A
                                    ; 初期状態では全てOFFとする
	CALL WRTPSG		                ; BIOS WRTPSG  PSGレジスタへデータを書き込み

    ; ■ ドライバステータス初期化
    LD A,SOUNDDRV_STATE_STOP
    LD (SOUNDDRV_STATE),A

    ; ■ ドライバワークエリア初期化
    LD HL,SOUNDDRV_WK_MIXING_TONE
    LD (HL),0

    LD HL,SOUNDDRV_WK_MIXING_NOISE
    LD (HL),0

    ; ■ BGM/SFXワークエリア初期化
    LD HL,SOUNDDRV_BGMWK
    LD B,SOUNDDRV_WORK_DATASIZE*6
SOUNDDRV_INIT_2:
    LD (HL),0
    INC HL
    DJNZ SOUNDDRV_INIT_2

    POP IY
    POP IX
    POP HL
    POP DE
    POP BC
    POP AF
    EI

    RET

; ----------------------------------------------------------------------------------------------------
; ワークエリア初期化処理
; IN  : A = プライオリティ値
;       HL = 設定対象のBGM/SFXデータのアドレス
;       IX = 設定対象のBGM/SFXトラックワークのアドレス
; ----------------------------------------------------------------------------------------------------
SOUNDDRV_INITWK:
    LD B,3                          ; チャンネル数

SOUNDDRV_INITWK_L1:
    LD A,(HL)                       ; A <- プライオリティ
    INC HL
    LD E,(HL)                       ; DE <- BGM/SFXデータの先頭アドレス
    INC HL
    LD D,(HL)

    ;   ウェイトカウンタ
    ;   最初に必ずゼロになるように、初期値を1とする
    LD (IX),1

    ;   次に読むBGM/SFXデータのアドレス
    ;   データの先頭アドレスを初期値とする
    LD (IX+1),E
    LD (IX+2),D

    ;   BGM/SFXデータの先頭アドレス
    LD (IX+3),E
    LD (IX+4),D

    ;   デチューン値
    LD (IX+5),0

    ;   ミキシング (bit0=Tont,bit1=Noise 0=On,1=Off)
    LD (IX+6),%11

    ;   プライオリティ
    LD (IX+15),A

    LD DE,16
    ADD IX,DE

    DJNZ SOUNDDRV_INITWK_L1

    RET
#ENDLIB

#LIB PSG_PLAY
; ====================================================================================================
; BGM PLAY
; IN  : HL = BGMデータの先頭アドレス
;            BGMデータの構成は以下とする
;              テンポ:1byte
;              トラック1のデータアドレス:2byte
;              トラック2のデータアドレス:2byte
;              トラック3のデータアドレス:2byte
; ====================================================================================================
SOUNDDRV_BGMPLAY:
    DI
    PUSH AF
    PUSH BC
    PUSH DE
    PUSH HL
    PUSH IX
    PUSH IY

    ; ■各チャンネルの初期設定
    PUSH HL
    XOR A
    CALL SOUNDDRV_GETWKADDR         ; HL <- 対象トラックのワークエリア先頭アドレス
    PUSH HL                         ; IX <- HL
    POP IX
    POP HL

    ; ■BGMトラックにBGMデータを設定
    CALL SOUNDDRV_INITWK

    LD A,(SOUNDDRV_STATE)
    OR SOUNDDRV_STATE_PLAY          ; サウンドドライバの状態を再生中にする
    LD (SOUNDDRV_STATE),A

    POP IY
    POP IX
    POP HL
    POP DE
    POP BC
    POP AF
    EI

    RET
#ENDLIB


#LIB PSG_SFX
; ====================================================================================================
; SFX PLAY
; IN  : HL = SFXデータの先頭アドレス
;            SFXデータの構成は以下とする
;              テンポ:1byte
;              トラック1のデータアドレス:2byte ゼロ=なし
;              トラック2のデータアドレス:2byte ゼロ=なし
;              トラック3のデータアドレス:2byte ゼロ=なし
; ====================================================================================================
SOUNDDRV_SFXPLAY:
    DI
    PUSH AF
    PUSH BC
    PUSH DE
    PUSH HL
    PUSH IX
    PUSH IY

    ; ■プライオリティ判定
    PUSH HL                         ; HL -> SPに退避
    XOR A                           ; BGMトラックの先頭トラック番号
    CALL SOUNDDRV_GETWKADDR         ; HL <- 対象トラックのワークエリア先頭アドレス
    PUSH HL
    POP IX                          ; IX <- HL
    LD B,(IX+15)                    ; B <- 再生中のBGMトラックのプライオリティ

    LD A,4                          ; SFXトラックの先頭トラック番号
    CALL SOUNDDRV_GETWKADDR         ; HL <- 対象トラックのワークエリア先頭アドレス
    PUSH HL
    POP IX                          ; IX <- HL
    LD A,(IX+15)                    ; A <- 再生中のSFXトラックのプライオリティ
    OR B                            ; A <- 再生中のBGM+SFXのプライオリティ値
    LD B,A                          ; B <- A (この後のCPの挙動を今までと同じにするための措置)
    POP HL                          ; HL <- SPから復元(SFXデータのアドレス)
    LD A,(HL)                       ; B <- SFXデータのプライオリティ
    CP B                            ; 再生中のBGM+SFXのプライオリティ値 - SFXデータのプライオリティ値
    JR C,SOUNDDRV_SFXPLAY_EXIT      ; キャリーフラグONの場合は処理せずに終了する

    ; ■SFXトラックにSFXデータを設定
    CALL SOUNDDRV_INITWK

    LD A,(SOUNDDRV_STATE)
    OR SOUNDDRV_STATE_PLAY          ; サウンドドライバの状態を再生中にする
    LD (SOUNDDRV_STATE),A

SOUNDDRV_SFXPLAY_EXIT:
    POP IY
    POP IX
    POP HL
    POP DE
    POP BC
    POP AF
    EI

    RET
#ENDLIB

#LIB PSG_STOP
; ====================================================================================================
; PLAY STOP
; ====================================================================================================
SOUNDDRV_STOP:
    DI
    PUSH AF
    PUSH BC
    PUSH DE
    PUSH HL
    PUSH IX
    PUSH IY

    LD A,(SOUNDDRV_STATE)
    AND SOUNDDRV_STATE_PAUSE        ; サウンドドライバの状態を停止にする
                                    ; 一時停止状態は保持するため、2とのANDを取る
                                    ; - 0 AND 2 -> 0
                                    ; - 1 AND 2 -> 0
                                    ; - 2 AND 2 -> 2
                                    ; - 3 AND 2 -> 2
    LD (SOUNDDRV_STATE),A

    ; ■全PSGチャンネルのボリュームを0にする
    LD B,3
SOUNDDRV_STOP_L1:
    LD E,0                          ; E <- データ(ボリューム)
    LD A,B                          ; A <- トラック番号
    ADD A,7                         ; PSGレジスタ8〜10に指定するため+7
    CALL WRTPSG
    DJNZ SOUNDDRV_STOP_L1

    ; ■全トラックのワークをクリア
    LD B,7                          ; BGMワークエリア＋SFXワークエリア(ダミー含む)
SOUNDDRV_STOP_L2:
    LD A,B
    SUB 1                           ; トラック番号は0〜なので-1する
    CALL SOUNDDRV_GETWKADDR
    PUSH HL
    POP IX
    LD (IX),$00                     ; ウェイトカウンタクリア
    LD (IX+3),$00                   ; トラックデータの先頭アドレスクリア
    LD (IX+4),$00
    LD (IX+15),$00                  ; プライオリティクリア
    DJNZ SOUNDDRV_STOP_L2

    POP IY
    POP IX
    POP HL
    POP DE
    POP BC
    POP AF
    EI

    RET
#ENDLIB


#LIB PSG_PAUSE
; ====================================================================================================
; PLAY PAUSE
; ====================================================================================================
SOUNDDRV_PAUSE:
    DI
    PUSH AF
;    PUSH BC
;    PUSH DE
;    PUSH HL
;    PUSH IX
;    PUSH IY

    LD A,(SOUNDDRV_STATE)
    CP SOUNDDRV_STATE_PAUSE
    JP NC,SOUNDDRV_RESUME_EXIT      ; 一時停止状態であれば抜ける

    XOR SOUNDDRV_STATE_PAUSE        ; 一時停止状態にする
    LD (SOUNDDRV_STATE),A

SOUNDDRV_PAUSE_EXIT:
;    POP IY
;    POP IX
;    POP HL
;    POP DE
;    POP BC
    POP AF
    EI

    RET
#ENDLIB

#LIB PSG_RESUME
; ====================================================================================================
; PLAY RESUME
; ====================================================================================================
SOUNDDRV_RESUME:
    DI
    PUSH AF
;    PUSH BC
;    PUSH DE
;    PUSH HL
;    PUSH IX
;    PUSH IY

    LD A,(SOUNDDRV_STATE)
    CP SOUNDDRV_STATE_PAUSE
    JP C,SOUNDDRV_RESUME_EXIT       ; 一時停止状態でなければ抜ける

    XOR SOUNDDRV_STATE_PAUSE        ; 一時停止状態を解除する
    LD (SOUNDDRV_STATE),A

    LD B,3
SOUNDDRV_RESUME_L1:
    LD A,B                          ; A <- B(ループカウンタ：1〜3)
    DEC A
    CALL SOUNDDRV_GETWKADDR         ; HL <- 対応するBGMトラックのワークエリアアドレス
    PUSH HL                         ; IX <- HL(最後なのでIXは壊してもOK)
    POP IX

    CALL SOUNDDRV_SETPSG_NOISETONE  ; ノイズトーン(PSGレジスタ6)設定
;    CALL SOUNDDRV_SETPSG_VOLUME     ; ボリューム(PSGレジスタ8～10)設定
;    CALL SOUNDDRV_SETPSG_TONE       ; トーン(PSGレジスタ0～5)設定
    DJNZ SOUNDDRV_RESUME_L1

SOUNDDRV_RESUME_EXIT:
;    POP IY
;    POP IX
;    POP HL
;    POP DE
;    POP BC
    POP AF
    EI

    RET
#ENDLIB


#LIB PSG_PROC
    RET

; ====================================================================================================
; DRIVER EXECUTE
; ====================================================================================================
SOUNDDRV_EXEC:
    ; ■サウンドドライバのステータス判定
    LD A,(SOUNDDRV_STATE)           ; A <- サウンドドライバの状態
    OR A
    JP Z,SOUNDDRV_EXIT              ; ゼロ(停止)なら抜ける
    CP SOUNDDRV_STATE_PAUSE
    JP NC,SOUNDDRV_ALLMUTE           ; 一時停止中の処理

    ; ■各トラックの処理
    XOR A
    CALL SOUNDDRV_CHEXEC
    LD A,1                          ; A <- 1(BGMトラック1=ChB)
    CALL SOUNDDRV_CHEXEC
    LD A,2                          ; A <- 2(BGMトラック2=ChC)
    CALL SOUNDDRV_CHEXEC
SOUNDDRV_EXEC_L1:
    LD A,4                          ; A <- 4(SFXトラック0=ChA)
    CALL SOUNDDRV_CHEXEC
    LD A,5                          ; A <- 5(SFXトラック1=ChB)
    CALL SOUNDDRV_CHEXEC
    LD A,6                          ; A <- 6(SFXトラック2=ChC)
    CALL SOUNDDRV_CHEXEC

    ; ■チャンネル全体の処理
    CALL SOUNDDRV_SETPSG_MIXING     ; ミキシング(PSGレジスタ7)設定処理

SOUNDDRV_EXIT:
;    RET
    JP SOUNDDRV_H_TIMI_BACKUP

; ----------------------------------------------------------------------------------------------------
; 一時停止中の処理
; チャンネル1〜3のボリュームをゼロにする
; ----------------------------------------------------------------------------------------------------
SOUNDDRV_ALLMUTE:
    XOR A
    LD E,A                          ; E = 書き込むデータ(ボリュームゼロ)
    LD A,8
    CALL WRTPSG
    LD A,9
    CALL WRTPSG
    LD A,10
    CALL WRTPSG
;    JP SOUNDDRV_EXIT
    JP SOUNDDRV_EXEC_L1

; ----------------------------------------------------------------------------------------------------
; トラックデータ再生処理
; IN  : A  = トラック番号(0〜2,4〜6)
; ----------------------------------------------------------------------------------------------------
SOUNDDRV_CHEXEC:
    LD D,A                          ; A -> D (Dレジスタにトラック番号を退避)

    CALL SOUNDDRV_GETWKADDR         ; HL <-トラックワークエリアの先頭アドレスを取得
    PUSH HL                         ; IX <- HL
    POP IX
    
    ; ■トラックデータの先頭アドレスをチェック
    LD A,(IX+3)
    OR (IX+4)
    RET Z                           ; トラックデータの先頭アドレス=ゼロ(未登録)なら抜ける    

    ; ■発声中の音のウェイトカウンタを減算
    DEC (IX)
    RET NZ                          ; -1した結果がゼロでない場合は発声中なので抜ける

SOUNDDRV_CHEXEC_L2:
    ; ■対象チャンネルの曲データを取得
    ;   発声が終了していたら、次のデータを取得する
    CALL SOUNDDRV_GETNEXTNATA       ; A <- シーケンスデータ
    JP Z,SOUNDDRV_CHEXEC_L3         ; ゼロフラグが立っている場合はL3へ
                                    ; (取得したデータが終端の時はゼロフラグが立っている)

SOUNDDRV_CHEXEC_L21:
    ; ■コマンドによる分岐
    CP 218                          ; データ=218(デチューン値)か
    JP Z,SOUNDDRV_CHEXEC_CMD218     ; デチューン値設定処理へ

    CP 217                          ; データ=217(ミキシング)か
    JP Z,SOUNDDRV_CHEXEC_CMD217     ; ミキシング設定処理へ

    CP 216                          ; データ=216(ノイズトーン)か
    JP Z,SOUNDDRV_CHEXEC_CMD216     ; ノイズトーン設定処理へ

    CP 253                          ; データ=253(ループ開始位置)か
    JP Z,SOUNDDRV_CHEXEC_CMD253     ; ループ開始位置設定処理へ

    CP 200                          ; データ=200〜(ボリューム)か
    JP NC,SOUNDDRV_CHEXEC_CMD20X    ; ボリューム設定処理へ

 
    ; ■データ=0〜190(トーンデータ)のときの処理
    ;   トーンテーブルから該当するデータを取得し、PSGレジスタ0〜5に設定する
    ;   次のデータを取得して、ウェイトカウンタに設定する
    LD B,0                          ; BC <- A(シーケンスデータ)
    LD C,A    
    LD HL,SOUNDDRV_TONETBL          ; HL <- トーンテーブルの先頭アドレス
    ADD HL,BC                       ; トーンデータは2byteなのでインデックスx2とする
    ADD HL,BC

    LD A,(HL)                       ; A <- トーンデータ(下位)
    SUB (IX+5)                      ; デチューン値を減算
    LD (IX+8),A                     ; トーンデータ(下位)をワークに保存
    INC HL
    LD A,(HL)                       ; A <- トーンデータ(上位)
    LD (IX+9),A                     ; トーンデータ(上位)をワークに保存


    LD A,D                          ; A <- D(トラック番号)
    CP 3
    JR NC,SOUNDDRV_CHEXEC_L22       ; 1(=SFX)の場合はL22へ

    ;   BGMトラックの時の処理
    ;   SFXトラックのワークに設定されているSFXデータの先頭アドレスを調べる
    ;   $0000でない場合はSFX再生中なので、トーンデータは設定せず、ウェイトカウンタの設定のみ行う
    ADD A,4                         ; SFXトラックを調べるためにトラック番号に+4する
    CALL SOUNDDRV_GETWKADDR         ; HLに対象トラックの先頭アドレスを取得

    INC HL                          ; @ToDo:トラックデータの先頭アドレスを求めることが多いので、ワークの持ち方を見直したい(毎回21ステートかかってる)
    INC HL
    INC HL

    LD A,(HL)                       ; 対象トラックの先頭アドレス=$0000か
    INC HL
    OR (HL)
    JR NZ,SOUNDDRV_CHEXEC_L23       ; ゼロでない場合はSFX再生中なのでBGMのトーンは設定せずL24へ

SOUNDDRV_CHEXEC_L22:
    CALL SOUNDDRV_SETPSG_TONE       ; トーン(PSGレジスタ0～5)設定

SOUNDDRV_CHEXEC_L23:
    ; ■該当チャンネルのウェイトカウンタ設定
    CALL SOUNDDRV_GETNEXTNATA       ; A <- シーケンスデータ
    LD (IX),A                       ; ワークにウェイトカウンタを設定

    RET

; ----------------------------------------------------------------------------------------------------
; データ終端処理
; ----------------------------------------------------------------------------------------------------
SOUNDDRV_CHEXEC_L3:
    ; ■現在のトラックがBGMかSFXかを判定する
    LD A,D                          ; A <- D(トラック番号)
    CP 3
    JP NC,SOUNDDRV_CHEXEC_L4        ; 1(=SFXトラック)ならL4へ

    ; ■BGMの再生が終了した場合
    LD (IX+3),$00                   ; ワークエリアのトラックデータ先頭アドレスをゼロに初期化
    LD (IX+4),$00
    LD (IX+15),$00                  ; プライオリティをゼロに初期化

    RET

SOUNDDRV_CHEXEC_L4:
    ; ■SFXの再生が終了した場合は、対象BGMトラックの状態を復元
    LD (IX+3),$00                   ; ワークエリアのトラックデータ先頭アドレスをゼロに初期化
    LD (IX+4),$00
    LD (IX+15),$00                  ; プライオリティをゼロに初期化

    LD A,D                          ; A <- D(トラック番号)
    AND %00000011                   ; トラック番号をチャンネル番号(0〜2)に変換
    CALL SOUNDDRV_GETWKADDR         ; HL <- 対応するBGMトラックのワークエリアアドレス
    PUSH HL                         ; IX <- HL(最後なのでIXは壊してもOK)
    POP IX

    LD A,(SOUNDDRV_STATE)
    AND SOUNDDRV_STATE_PAUSE
    RET NZ

    CALL SOUNDDRV_SETPSG_NOISETONE  ; ノイズトーン(PSGレジスタ6)設定
    CALL SOUNDDRV_SETPSG_VOLUME     ; ボリューム(PSGレジスタ8～10)設定
    CALL SOUNDDRV_SETPSG_TONE       ; トーン(PSGレジスタ0～5)設定

;SOUNDDRV_CHEXEC_EXIT:
    RET

; ----------------------------------------------------------------------------------------------------
; コマンド：200〜215（ボリューム）設定
; ----------------------------------------------------------------------------------------------------
SOUNDDRV_CHEXEC_CMD20X:
    ; ■ボリューム設定処理
    ;   コマンドデータ-200がボリューム値になるため、計算してワークエリアに設定する
    ;   そして次のシーケンスデータの処理を行う
    SUB 200                         ; A=A-200(0〜15のボリューム値にする)
    LD (IX+7),A                     ; ボリューム値をワークに保存

    LD A,D                          ; A <- D(トラック番号)

    ; ■現在のトラックがBGMかSFXかを判定する
    CP 3
    JP NC,SOUNDDRV_CHEXEC_CMD20X_L1 ; 1(=SFXトラック)ならL1へ

    ADD A,4                         ; SFXトラックを調べるためにトラック番号にA=A+4する
    CALL SOUNDDRV_GETWKADDR         ; HLに対象トラックの先頭アドレスを取得

    INC HL                          ; @ToDo:トラックデータの先頭アドレスを求めることが多いので、ワークの持ち方を見直したい(毎回21ステートかかってる)
    INC HL
    INC HL

    LD A,(HL)
    INC HL
    OR (HL)                         ; 対象トラックの先頭アドレス=$0000か
    JR NZ,SOUNDDRV_CHEXEC_CMD20X_L2 ; ゼロでない場合はSFX再生中なのでCMD20X_1へ

SOUNDDRV_CHEXEC_CMD20X_L1:
    CALL SOUNDDRV_SETPSG_VOLUME     ; PSGレジスタ8～10設定

SOUNDDRV_CHEXEC_CMD20X_L2:
    JP SOUNDDRV_CHEXEC_L2

; ----------------------------------------------------------------------------------------------------
; コマンド：217（ミキシング値）設定
;   次のシーケンスデータを取得して、ワークエリアに設定すると同時にPSGレジスタ7に設定する
;   そして次のシーケンスデータの処理を行う
; ----------------------------------------------------------------------------------------------------
SOUNDDRV_CHEXEC_CMD217:
    CALL SOUNDDRV_GETNEXTNATA       ; A <- シーケンスデータ(ミキシング値)
    LD (IX+6),A

    JP SOUNDDRV_CHEXEC_L2

; ----------------------------------------------------------------------------------------------------
; コマンド：216（ノイズトーン値）設定
;   次のシーケンスデータを取得して、ワークエリアに設定する
;   そして次のシーケンスデータの処理を行う
; ----------------------------------------------------------------------------------------------------
SOUNDDRV_CHEXEC_CMD216:
    CALL SOUNDDRV_GETNEXTNATA       ; A <- シーケンスデータ(ノイズトーン値)
    LD (IX+10),A                    ; ノイズトーン値をワークに保存
    CALL SOUNDDRV_SETPSG_NOISETONE  ; ノイズトーン(PSGレジスタ6)設定

    JP SOUNDDRV_CHEXEC_L2

; ----------------------------------------------------------------------------------------------------
; コマンド：218（デチューン値）設定
;   次のシーケンスデータを取得して、ワークエリアに設定する
;   そして次のシーケンスデータの処理を行う
; ----------------------------------------------------------------------------------------------------
SOUNDDRV_CHEXEC_CMD218:
    CALL SOUNDDRV_GETNEXTNATA       ; A <- シーケンスデータ(デチューン値)
    LD (IX+5),A

    JP SOUNDDRV_CHEXEC_L2

; ----------------------------------------------------------------------------------------------------
; コマンド：253（ループ開始位置）設定
;   現在のアドレス+1をワークに設定する
;   そして次のシーケンスデータの処理を行う
; ----------------------------------------------------------------------------------------------------
SOUNDDRV_CHEXEC_CMD253:
    LD C,(IX+1)                     ; BC <- この時点で次のシーケンスデータのアドレスが設定されている
    LD B,(IX+2)
    
    LD (IX+3),C                     ; トラックデータの先頭アドレスを書き換える
    LD (IX+4),B

    JP SOUNDDRV_CHEXEC_L2

; ----------------------------------------------------------------------------------------------------
; トーン(PSGレジスタ0〜5)設定
;   チャンネルA:PSGレジスタ0,1
;   チャンネルB:PSGレジスタ2,3
;   チャンネルC:PSGレジスタ4,5
;   現在のワークの設定値からPSGレジスタ0～5の設定値を求め、WRTPSGを実行する
; IN  : D = トラック番号
;       IX = 対象トラックのワークエリア先頭アドレス
; ----------------------------------------------------------------------------------------------------
SOUNDDRV_SETPSG_TONE:
    LD A,D                          ; A <- Dレジスタに退避した値(トラック番号)
    AND %00000011                   ; 下位2ビットをチャンネル番号とする
    ADD A,A                         ; PSGレジスタ番号=0/2/4(下位8ビット)
    LD E,(IX+8)                     ; E <- ワークのトーン(下位)
    CALL WRTPSG

    INC A                           ; PSGレジスタ番号=1/3/5(上位4ビット)
    LD E,(IX+9)                     ; E <- ワークのトーン(上位)
    CALL WRTPSG

    RET

; ----------------------------------------------------------------------------------------------------
; ノイズトーン(PSGレジスタ6)設定処理
;   全チャンネル共通
;   現在のワークの設定値からPSGレジスタ6の設定値を求め、WRTPSGを実行する
; IN  : IX = 対象トラックのワークエリア先頭アドレス
; ----------------------------------------------------------------------------------------------------
SOUNDDRV_SETPSG_NOISETONE:
    LD E,(IX+10)
    LD A,6
    CALL WRTPSG

    RET

; ----------------------------------------------------------------------------------------------------
; ボリューム(PSGレジスタ8〜10)設定処理
;   チャンネルA:PSGレジスタ8
;   チャンネルB:PSGレジスタ9
;   チャンネルC:PSGレジスタ10
;   現在のワークの設定値からPSGレジスタ8～10の設定値を求め、WRTPSGを実行する
; IN  : D = トラック番号
;       IX = 対象トラックのワークエリア先頭アドレス
; ----------------------------------------------------------------------------------------------------
SOUNDDRV_SETPSG_VOLUME:
    LD A,D                          ; A <- トラック番号(0〜2, 4〜6)
    AND %00000011                   ; 下位2ビットをチャンネル番号とする
    ADD A,8                         ; PSGレジスタ8〜10に指定するため+8
    LD E,(IX+7)
    CALL WRTPSG

    RET

; ----------------------------------------------------------------------------------------------------
; ミキシング(PSGレジスタ7)設定処理
;   全チャンネル共通
;   現在のワークの設定値からPSGレジスタ7の設定値を求め、WRTPSGを実行する
;   レジスタ7への設定値は以下となる(0=On,1=Off)
;     xx000000
;       |||||bit0:ChA Tone
;       ||||bit1:ChB Tone
;       |||bit2:ChC Tone
;       ||bit3:ChA Noise
;       |bit4:ChB Noise
;       bit5:ChC Noise
;   各トラックのワークには以下で設定
;     00
;     |bit0:Tone
;     bit1:Noise
; ----------------------------------------------------------------------------------------------------
SOUNDDRV_SETPSG_MIXING:
    LD B,3                          ; ループ回数

    XOR A
    LD (SOUNDDRV_WK_MIXING_TONE),A  ; A -> PSGレジスタ7のWK(bit0〜2:Tone設定用)初期化
    LD (SOUNDDRV_WK_MIXING_NOISE),A ; A -> PSGレジスタ7のWK(bit3〜5:Noise設定用)初期化

SOUNDDRV_SETPSG_MIXING_L1:
    ; ■各トラックのミキシング値のアドレスを設定する
    ;   Ch2,1,0の順に処理する
    ;   SFXトラックのトラックデータ先頭アドレスを求める
    LD A,B                          ; A <- B(1〜3)
    ADD A,3                         ; Aに+3して、トラック番号4〜6(SFXトラック1〜3)とする
    CALL SOUNDDRV_GETWKADDR         ; HL <- SFXワークエリアの先頭アドレス
    INC HL                          ; @ToDo:トラックデータの先頭アドレスを求めることが多いので、ワークの持ち方を見直したい(毎回21ステートかかってる)
    INC HL
    INC HL

    ;   SFXトラックのトラックデータ先頭アドレスを判定
    LD A,(HL)                       ; トラックデータの先頭アドレスが$0000か
    INC HL
    OR (HL)
    JR NZ,SOUNDDRV_SETPSG_MIXING_L2     ; ゼロでないならSFXトラックが設定されているので、次の処理へ

    ;   BGMトラックのトラックデータ先頭アドレスを求める
    LD A,B                          ; A <- B(1〜3)
    SUB 1                           ; Aを-1して、トラック番号0〜2とする
    CALL SOUNDDRV_GETWKADDR         ; HL <- BGMワークエリアの先頭アドレス
    INC HL                          ; @ToDo:トラックデータの先頭アドレスを求めることが多いので、ワークの持ち方を見直したい(毎回21ステートかかってる)
    INC HL
    INC HL

    ;   BGMトラックのトラックデータ先頭アドレスを判定
    LD A,(HL)                       ; トラックデータの先頭アドレスが$0000か
    INC HL
    OR (HL)
    JR NZ,SOUNDDRV_SETPSG_MIXING_L2     ; ゼロでないならBGMトラックが設定されているので、次の処理をスキップ

    ; BGMもSFXも未設定の場合は、ミキシング値を%11(Noise,Tone=Off)にする
    LD D,%11
    JR SOUNDDRV_SETPSG_MIXING_L3

SOUNDDRV_SETPSG_MIXING_L2:
    ; ■各トラックのミキシング値を取得してワークに設定する
    INC HL
    INC HL
    LD D,(HL)                       ; D <- 対象トラックのミキシング値

SOUNDDRV_SETPSG_MIXING_L3:
    ;   Toneのミキシング値
    SRL D                           ; Dレジスタを1ビット右シフト 元の値のbit0→キャリーフラグ
    LD A,(SOUNDDRV_WK_MIXING_TONE)  ; A <- PSGレジスタ7のWK(bit0〜2:Tone設定用)
    RLA                             ; Aレジスタを1ビット左ローテート bit0←キャリーフラグ
    LD (SOUNDDRV_WK_MIXING_TONE),A  ; A -> PSGレジスタ7のWK(bit0〜2:Tone設定用)

    ;   Noiseのミキシング値
    SRL D                           ; Dレジスタを1ビット右シフト 元の値のbit1→キャリーフラグ
    LD A,(SOUNDDRV_WK_MIXING_NOISE) ; A <- PSGレジスタ7のWK(bit3〜5:Noise設定用)
    RLA                             ; Aレジスタを1ビット左ローテート bit0←キャリーフラグ
    LD (SOUNDDRV_WK_MIXING_NOISE),A ; A -> PSGレジスタ7のWK(bit3〜5:Noise設定用)

    DJNZ SOUNDDRV_SETPSG_MIXING_L1

    ; ■レジスタ7に設定する値を求める
    LD A,(SOUNDDRV_WK_MIXING_TONE)  ; A <- PSGレジスタ7のWK(bit0〜2:Tone設定用)
    LD E,A                          ; E <- A

    LD A,(SOUNDDRV_WK_MIXING_NOISE) ; A <- PSGレジスタ7のWK(bit0〜2:Tone設定用)
    SLA A                           ; 左3bitシフト → bit2〜0のデータをbit5〜3に移動する
    SLA A
    SLA A
    OR E                            ; Toneの値を加算
    OR %10000000                    ; bit7〜6を設定
    LD E,A
    LD A,7
    CALL WRTPSG

    RET

; ----------------------------------------------------------------------------------------------------
; 次に読むトラックデータのアドレスからデータを取得する
; 同時に、トラックデータの取得アドレスも更新する
; データが終端(=$FF)の場合は、トラックデータの取得アドレスを先頭アドレスに戻す
; IN  : IX = トラックワークエリアの先頭アドレス
; OUT : A = トラックデータ
; ----------------------------------------------------------------------------------------------------
SOUNDDRV_GETNEXTNATA:
    ; ■トラックデータを取得
    LD C,(IX+1)                     ; BC <- トラックデータの取得アドレス
    LD B,(IX+2)
    LD A,(BC)                       ; A <- 曲データ

    ; ■終端判定
    CP $FF                          ; データ=$FFか
    RET Z                           ; 終端の場合はそのまま処理終了

    ; ■ループ判定
    CP $FE                          ; データ=$FEか
    JR NZ,SOUNDDRV_GETNEXTNATA_L2   ; $FEでなければL2へ

    ; ■トラックデータをループ先頭に戻す
    INC A                           ; ゼロフラグをクリアする
                                    ; (Aレジスタに無条件に1を加算、ここに来る前提でAは$FF未満なのでゼロフラグは必ずOFFになる)
    LD C,(IX+3)                     ; BC <- トラックデータの先頭アドレス
    LD B,(IX+4)
    LD A,(BC)                       ; Aレジスタにトラックデータを読み直す

SOUNDDRV_GETNEXTNATA_L2:
    ; ■次に読むトラックデータのアドレスを+1して保存
    INC BC
    LD (IX+1),C                     ; BC -> 次に読むトラックデータのアドレス
    LD (IX+2),B

    RET

; ----------------------------------------------------------------------------------------------------
; BGM/SFXワークエリアのアドレスを求める
; IN  : A = トラック番号(0〜2,4〜6)
; OUT : HL = 対象トラックのワークエリア先頭アドレス
; ----------------------------------------------------------------------------------------------------
SOUNDDRV_GETWKADDR:
    PUSH BC
    LD HL,SOUNDDRV_BGMWK            ; HL <- BGMワークエリアの先頭アドレス

    OR A                            ; ゼロか
    JR Z,SOUNDDRV_GETWKADDR_L1      ; ゼロなら計算不要なのでL2へ

    SLA A                           ; A=A*16(ワークエリアのサイズ)
    SLA A
    SLA A
    SLA A

SOUNDDRV_GETWKADDR_L1:
    LD B,0
    LD C,A
    ADD HL,BC                       ; HL <- 対象トラックのワークエリアのアドレス

    POP BC
    RET


; ====================================================================================================
; 定数エリア
; romに格納される
; ====================================================================================================
; SECTION rodata_user

; ; ■BIOSアドレス定義
; INCLUDE "include/msxbios.inc"
; 
; ; ■システムワークエリア定義
; INCLUDE "include/msxsyswk.inc"

SOUNDDRV_STATE_STOP:    EQU 0       ; サウンドドライバ状態：停止
SOUNDDRV_STATE_PLAY:    EQU 1       ; サウンドドライバ状態：演奏中
SOUNDDRV_STATE_PAUSE:   EQU 2       ; サウンドドライバ状態：一時停止

SOUNDDRV_WORK_DATASIZE: EQU 16      ; サウンドドライバ1chのワークエリアサイズ


; ----------------------------------------------------------------------------------------------------
; トーンテーブル
; ----------------------------------------------------------------------------------------------------
SOUNDDRV_TONETBL:
;          C   C+    D   D+    E    F   F+    G   G+    A   A+    B
	dw  3420,3229,3047,2876,2715,2562,2419,2283,2155,2034,1920,1812 ;o1  0〜 11
	dw  1710,1614,1524,1438,1357,1281,1209,1141,1077,1017, 960, 906 ;o2 12〜 23
	dw   855, 807, 762, 719, 679, 641, 605, 571, 539, 508, 480, 453 ;o3 24〜 35
	dw   428, 404, 381, 360, 339, 320, 302, 285, 269, 254, 240, 226 ;o4 36〜 47
	dw   214, 202, 190, 180, 170, 160, 151, 143, 135, 127, 120, 113 ;o5 48〜 59
	dw   107, 101,  95,  90,  85,  80,  76,  71,  67,  64,  60,  57 ;o6 60〜 71
	dw    53,  50,  48,  45,  42,  40,  38,  36,  34,  32,  30,  28 ;o7 72〜 83
	dw    27,  25,  24,  22,  21,  20,  19,  18,  17,  16,  15,  14 ;o8 84〜 95



#ENDLIB

#LIB PSG_END
    DI

    CALL PSG_STOP

    ; ■H.TIMIを戻す
    LD HL,SOUNDDRV_H_TIMI_BACKUP
    LD DE,H_TIMI
    LD BC,5
    LDIR

    EI
    RET
#ENDLIB
