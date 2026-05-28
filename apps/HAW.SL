/*

    Welcome to H.A.W. !!

        metal-fight

                            */

//    ORG $5000;

// offset  $6000;
// org     $3000;
// work    $d000;

const   _tilesw    =  1 ,
        _sincos    =  1 ,
        _three     =  1 ,
        _plotsw    =  0 ,
        _float     =  0 ,
        _ghin      =  7 ,

        M_WAIT     =  45 ,

        ROPE_MAX   =  9 ,
        BOX_MAX    =  9 ,
        BOBULE_MAX =  9 ;

array   ropex[ ROPE_MAX ],
        ropey[ ROPE_MAX ],
        roper[ ROPE_MAX ],
        ropea[ ROPE_MAX ],
        ropes[ ROPE_MAX ],
        rope_mx[ ROPE_MAX ],
        rope_my[ ROPE_MAX ],
        rope_hx[ ROPE_MAX ],
        rope_hy[ ROPE_MAX ],
        rope_clsx[ ROPE_MAX ],
        rope_clsy[ ROPE_MAX ],
        rope_clsmx[ ROPE_MAX ],
        rope_clsmy[ ROPE_MAX ],
        rope_clshx[ ROPE_MAX ],
        rope_clshy[ ROPE_MAX ],

        box_x[ BOX_MAX ],
        box_y[ BOX_MAX ],
        box_xs[ BOX_MAX ],
        box_ys[ BOX_MAX ],
        box_id[ BOX_MAX ],
        box_clsx[ BOX_MAX ],
        box_clsy[ BOX_MAX ],

        bob_x[ BOBULE_MAX ],
        bob_y[ BOBULE_MAX ],
        bob_cx[ BOBULE_MAX ],
        bob_cy[ BOBULE_MAX ],
        bob_ct[ BOBULE_MAX ],

        box_tile[3][7]=[ $ff , $88 , $88 , $88 ,
                         $88 , $ff , $88 , $88 ,
                         $88 , $88 , $ff , $88 ,
                         $88 , $88 , $88 , $ff ,

                         $ff , $44 , $44 , $44 ,
                         $44 , $ff , $44 , $44 ,
                         $44 , $44 , $ff , $44 ,
                         $44 , $44 , $44 , $ff ,

                         $ff , $22 , $22 , $22 ,
                         $22 , $ff , $22 , $22 ,
                         $22 , $22 , $ff , $22 ,
                         $22 , $22 , $22 , $ff ,

                         $ff , $11 , $11 , $11 ,
                         $11 , $ff , $11 , $11 ,
                         $11 , $11 , $ff , $11 ,
                         $11 , $11 , $11 , $ff ],

        MUSADR[]:SOUNDDATA; 

var     mx , my , cls_mx , cls_my ,
        vec_x , vec_y , scr_x , scr_y , on_sw , cat_sw ,
        blk_max , rop_max , sync_ct,
        dead_line , dead_cline , gvec ;


#include GRAPH.LIB

ARRAY BYTE FMMUSADR[4096];

main()
var     i , J;
[
    MEM[$F850] = $10;

    // FOPEN(0,"MUS.BIN",4);
    // FREAD(0,FMMUSADR,4096);
    // FCLOSE(0);

    // FM_INIT();
    // FM_PLAY(FMMUSADR);

    // FOPEN(0,"MAGIC.SOS",4);
    // FREAD(0,$B000,$2000);
    // FCLOSE(0);

    @init();
    _kstep = 30;
    sync_ct = 0;
    for i=0 to BOBULE_MAX [ bob_ct[ i ] = 0; ]
    @mode(2,2);
    // for i=0 to 6 [
    //     @kyu((i-3).*.630 , (i-3).*.200 , 3000-i.*.200 , 450 );
    // ]
    @crtkn(0,0,0);
    _demode   =   2;
    blk_max   =   9;
    rop_max   =   9;

    for i=0 to 9 [
        ropex[i]  = 750 + 900 * i;
        ropey[i]  =  80 - 120 * i;
        roper[i]  =   0;
        ropea[i]  =  10;
        ropes[i]  =   1;

        box_x[i]  = 110 + 900 * i;
        box_y[i]  = 190 -  80 * i;
        box_xs[i] = 619;
        box_ys[i] = 200;
    ]
    box_id[       0 ] = 0;
    box_id[       1 ] = 3;
    box_id[       2 ] = 4;
    box_id[       3 ] = 5;
    box_id[       4 ] = 0;
    box_id[       5 ] = 1;
    box_id[       6 ] = 1;
    box_id[       7 ] = 1;
    box_id[       8 ] = 0;
    box_id[       9 ] = 0;
    box_id[ blk_max ] = 2;
    dead_line         = 200;
    mx    =  319;
    my    =  100;
    vec_y =  -15;
    vec_x =    0;

    PSG_INIT();
    PSG_PLAY(MUSADR);

    while( inkey(0) <> 'E' ) [
        ball();
        rope(rop_max , scr_x , scr_y);
        box(blk_max , scr_x , scr_y);
        @grad(6);
        @circle(mx , my , 20);
        d_line(scr_y);
        bobule(BOBULE_MAX , scr_x , scr_y);
//      gyajirusi();
        @crtkn( 5 , 1 , 0 );
//      gyajirusi_cls();
        ball_cls();
        rope_cls( rop_max );
        box_cls( blk_max );
        bobule_cls( BOBULE_MAX );
        if d_line_cls() [
            for i=0 to 30 [
                rtile();
                d_line(-1);
                rope(rop_max , 0 , -5);
                box(blk_max , 0 , -10);
                rtile();
                @circle(mx , my , i*2 + 20);
                for j = 0 to 10 [
                    @line( mx ,
                           my ,
                           mx + rnd(i*4) - i*2 ,
                           my + rnd(i*2) - i);
                ]
                    @crtkn( 3 , 2 , 1 );
                d_line_cls();
                rope_cls( rop_max );
                box_cls( blk_max );
            ]
            @palet(0 , 6 , 6 , 2 , 6 , 6 , 7 , 2); wait(40);
            @palet(1 , 6 , 6 , 2 , 6 , 6 , 7 , 2); wait(40);
            @palet(2 , 3 , 3 , 3 , 3 , 3 , 7 , 3); wait(40);
            @palet(3 , 3 , 3 , 3 , 3 , 3 , 7 , 3); wait(40);
            @palet(4 , 2 , 2 , 6 , 2 , 2 , 7 , 6); wait(40);
            @palet(5 , 2 , 2 , 6 , 2 , 2 , 7 , 6); wait(40);
            @palet(6 , 1 , 1 , 7 , 1 , 1 , 7 , 7); wait(40);
            @palet(7 , 0 , 0 , 7 , 0 , 0 , 7 , 7); wait(99);
            exit;
        ]
    ]
    PSG_STOP();
    PSG_END();
    @palet(0,0,0,0,0,0,0,0);
]


rope( rope_su , scroll_x , scroll_y )
var     rope_x1,
        rope_y1,
        i;
[
    for i=0 to rope_su [
        rope_clsx[ i ]  = ropex[ i ];
        rope_clsy[ i ]  = ropey[ i ];
        rope_clsmx[ i ] = rope_mx[ i ];
        rope_clsmy[ i ] = rope_my[ i ];
        rope_clshx[ i ] = rope_hx[ i ];
        rope_clshy[ i ] = rope_hy[ i ];

        rope_x1 = ropex[ i ] + scroll_x;
        rope_y1 = ropey[ i ] + scroll_y;
        roper[ i ] = roper[ i ] + ropea[ i ];
        if ( roper[ i ] .>.    75  ) or
           ( roper[ i ] .<. 0 -75  ) [
            roper[ i ] = roper[ i ] - ropea[ i ];
            ropea[ i ] = 0 - ropea[ i ];
        ]
        rope_mx[ i ] = rope_x1 +
                       @isin( roper[ i ] ./. 2 , ropes[ i ] * 40 ).*.2;
        rope_my[ i ] = rope_y1 +
                       @icos( roper[ i ] ./. 2 , ropes[ i ] * 40 ).*.3./.2;
        rope_hx[ i ] = rope_x1 +
                       @isin( roper[ i ]       , ropes[ i ] * 80 ).*.2;
        rope_hy[ i ] = rope_y1 +
                       @icos( roper[ i ]       , ropes[ i ] * 80 ).*.3./.2;
        if (rope_x1 .<. 1200) and
           (rope_x1 .>. -600) and
           (rope_y1 .<.  200) and
           (rope_y1 .>. -300) [

            @spline( rope_x1      , rope_y1      ,
                     rope_mx[ i ] , rope_my[ i ] ,
                     rope_hx[ i ] , rope_hy[ i ]
            );
        ]
        ropex[ i ] = rope_x1;
        ropey[ i ] = rope_y1;
    ]
]


rope_cls( rope_su )
var     i;
[
    @mode( 0 , 1-_mscreen );
    for i=0 to rope_su [
        if (rope_clsx[ i ] .<. 1200) and
           (rope_clsx[ i ] .>. -600) and
           (rope_clsy[ i ] .<.  200) and
           (rope_clsy[ i ] .>. -300) [

            @spline(  rope_clsx[ i ] ,  rope_clsy[ i ] ,
                     rope_clsmx[ i ] , rope_clsmy[ i ] ,
                     rope_clshx[ i ] , rope_clshy[ i ]
            );
        ]
    ]
    @mode( 2 , 1-_mscreen);
]


box( box_su , scroll_x , scroll_y )
var     i , mov_x , mov_y;
[
    sync_ct = (++sync_ct mod 4);
    for i=0 to box_su [
        box_clsx[ i ] = box_x[ i ];
        box_clsy[ i ] = box_y[ i ];
        box_x[ i ] = box_x[ i ] + scroll_x;
        box_y[ i ] = box_y[ i ] + scroll_y;
        if ( box_x[ i ]               .<.  640 ) and
           ( box_x[ i ] + box_xs[ i ] .>.    -1) and
           ( box_y[ i ]               .<.  200 ) and
           ( box_y[ i ] + box_ys[ i ] .>.    -1) [

            case box_id[ i ] of [
             0 to 1 @box(  box_x[ i ] ,
                           box_y[ i ] ,
                           box_x[ i ] + box_xs[ i ] ,
                           box_y[ i ] + box_ys[ i ]
                    );
                2 [ @grad(rnd(3) + 4);
                    @full( box_x[ i ] ,
                           box_y[ i ] ,
                           box_x[ i ] + box_xs[ i ] ,
                           box_y[ i ] + box_ys[ i ]
                    ); ]

          3 to 10 [ case box_id[ i ] of [
                        3  [ mov_x = 0;           mov_y = 3 - sync_ct; ]
                        4  [ mov_x =     sync_ct; mov_y = 3 - sync_ct; ]
                        5  [ mov_x =     sync_ct; mov_y = 0;           ]
                        6  [ mov_x =     sync_ct; mov_y =     sync_ct; ]
                        7  [ mov_x = 0;           mov_y =     sync_ct; ]
                        8  [ mov_x = 3 - sync_ct; mov_y =     sync_ct; ]
                        9  [ mov_x = 3 - sync_ct; mov_y = 0;           ]
                       10  [ mov_x = 3 - sync_ct; mov_y = 3 - sync_ct; ]
                    ]
                    _tile1 = box_tile[ mov_x ][ mov_y * 2    ];
                    _tile2 = box_tile[ mov_x ][ mov_y * 2 +1 ];
                    @full( box_x[ i ] ,
                           box_y[ i ] ,
                           box_x[ i ] + box_xs[ i ] ,
                           box_y[ i ] + box_ys[ i ] );
                  ]
            ]
        ]
    ]

]


box_cls( box_su )
var     i;
[
    @mode( 0 , 1-_mscreen );
    for i=0 to box_su [
        if ( box_clsx[ i ]               .<.  640 ) and
           ( box_clsx[ i ] + box_xs[ i ] .>.    -1) and
           ( box_clsy[ i ]               .<.  200 ) and
           ( box_clsy[ i ] + box_ys[ i ] .>.    -1) [

            case box_id[ i ] of [
             0 to 1 @box(  box_clsx[ i ] ,
                           box_clsy[ i ] ,
                           box_clsx[ i ] + box_xs[ i ] ,
                           box_clsy[ i ] + box_ys[ i ]
                    );
          2 to 10 [ @grad(0);
                    @full( box_clsx[ i ] ,
                           box_clsy[ i ] ,
                           box_clsx[ i ] + box_xs[ i ] ,
                           box_clsy[ i ] + box_ys[ i ]
                    ); ]
            ]
        ]
    ]
    @mode( 2 , 1-_mscreen );
]


ball()
[
    cls_mx = mx;
    cls_my = my;
    ball_move();
    ball_kansei();
    ball_hant();
    ball_zahyou();
]


ball_move()
var     key;
[
    key = inkey(0);
    case key of [
        '4'     vec_x = vec_x - 6;
        '6'     vec_x = vec_x + 6;
        '8'     if ( cat_sw == 1 ) or
                   ( on_sw  == 1 ) [
                    vec_y = vec_y - 26;
                    on_sw  = 0;
                    if ( cat_sw == 1) cat_sw = -3;
                ]
        '2'     if ( vec_y .>. 0 ) vec_y = vec_y + 3;
    ]
]


ball_kansei()
[
    vec_y = vec_y + 3;
    if ( vec_y .>.  30 ) vec_y =  30;
    if ( vec_y .<. -30 ) vec_y =  vec_y + 3;
    if ( vec_x .>.  30 ) vec_x =  vec_x - 6;
    if ( vec_x .<. -30 ) vec_x =  vec_x + 6;

    if ( on_sw ==    1 ) vec_x = vec_x + sgn( 0 - vec_x )*3;

    if (cat_sw <> 1) [
        mx = mx + vec_x;
        my = my + vec_y;
    ]
]


ball_hant()
[
    rope_hant();
    block_hant();
]


rope_hant()
var     i;
[
    if ( cat_sw > 32767 ) [
        cat_sw++;
        return;
    ]
    cat_sw = 0;
    for i=0 to rop_max [
        if ( abs(mx - rope_hx[ i ]) < 70 ) and
           ( abs(my - rope_hy[ i ]) < 50 ) [
            mx = rope_hx[ i ];
            my = rope_hy[ i ];
            vec_x  = ropes[ i ] .*. ropea[ i ] .*. 3;
            vec_y  = 0;
            cat_sw = 1;
        ]
    ]
]


block_hant()
var     i;
[
    if (cat_sw == 1) return;
    on_sw = 0;
    for i=0 to blk_max [
        if ( mx      .>.              box_x[ i ]  ) and
           ( mx      .<. box_x[ i ] + box_xs[ i ] ) and
           ( my + 10 .>.              box_y[ i ]  ) and
           ( my - 10 .<. box_y[ i ] + box_ys[ i ] ) [
            case box_id[ i ] of [
                0 [ my = box_y[ i ] - 10;
                    vec_y = 0;
                    on_sw = 1;
                  ]
           1 to 2 [ vec_y = vec_y - 6;
                    vec_x = vec_x + sgn( 0 - vec_x )*3;
                    my    = my    - 5;
                    bobule_out();
                    if (my + vec_y .<. box_y[ i ] + 10) [
                        vec_y = 0;
                        on_sw = 1;
                    ]
                  ]
                3 [                    vec_y = vec_y - 6; on_sw = 1; ]
                4 [ vec_x = vec_x + 6; vec_y = vec_y - 6; on_sw = 1; ]
                5 [ vec_x = vec_x + 6; vec_y = vec_y - 3; on_sw = 1; ]
                6 [ vec_x = vec_x + 6; vec_y = vec_y + 3; on_sw = 1; ]
                7 [                    vec_y = vec_y + 3; on_sw = 1; ]
                8 [ vec_x = vec_x - 6; vec_y = vec_y + 3; on_sw = 1; ]
                9 [ vec_x = vec_x - 6; vec_y = vec_y - 3; on_sw = 1; ]
               10 [ vec_x = vec_x - 6; vec_y = vec_y - 6; on_sw = 1; ]
            ]
        ]
    ]
]


ball_zahyou()
[
    scr_x = 0;
    scr_y = 0;
    if (mx .>. 489) [
        scr_x = 489 - mx;
        mx    = 489;
    ]
    if (mx .<. 150) [
        scr_x = 150 - mx;
        mx    = 150;
    ]
    if (my .>. 149) [
        scr_y = 149 - my;
        my    = 149;
    ]
    if (my .<.  50) [
        scr_y =  50 - my;
        my    =  50;
    ]
]

ball_cls()
[
    @grad(0);
    @circle( cls_mx , cls_my , 20 );
]

bobule( bobule_su , sx , sy )
var     i;
[
    for i=0 to BOBULE_MAX [
        if (bob_ct[ i ] > 0) [
            bob_cx[ i ] = bob_x[ i ];
            bob_cy[ i ] = bob_y[ i ];
            bob_x[ i ] = bob_x[ i ] + sx;
            bob_y[ i ] = bob_y[ i ] + sy - 2;
            @grad(rnd(5) + 1);
            IF (bob_ct[ i ] > 1) [
                @circle(bob_x[ i ] , bob_y[ i ] , bob_ct[ i ]);
            ]
        ]
    ]
]

bobule_cls( bobule_su )
var     i;
[
    for i=0 to BOBULE_MAX [
        if (bob_ct[ i ] > 0) [
            @grad(0);
            @circle(bob_cx[ i ] , bob_cy[ i ] , bob_ct[ i ] + 3);
            bob_ct[ i ]--;
        ]
    ]
]

bobule_out()
var     i,f;
[
    for i=0 to BOBULE_MAX [
        if (bob_ct[ i ] == 0) [
            bob_x[ i ] = mx - 15 + rnd(30);
            bob_y[ i ] = my + 10;
            bob_ct[ i ] = BOBULE_MAX;
            exit;
        ]
    ]
]

d_line(sy)
[
    dead_cline = dead_line;
    dead_line = dead_line + sy;
    if (dead_line .<. 200) [
        rtile();
        @full(0 , dead_line , 639 , 199);
    ]
]

d_line_cls()
[
    if (dead_cline .<. 200) [
        @grad(0);
        @full(0 , dead_cline , 639 , 199);
        if (dead_line .<. my) [
            return(1);
        ]
    ]
    return(0);
]

rtile()
[
    _tile1 = rnd(65535);
    _tile2 = rnd(65535);
]

wait(wt)
[
    wt = wt * M_WAIT;
    while(--wt <> 0) []
]

gyajirusi()
[
    gvec = @IATN(
        ( box_x[ blk_max ] + box_xs[ blk_max ] / 2 ) - mx ,
        ( box_y[ blk_max ] + box_ys[ blk_max ] / 2 ) - my
    );
    @grad(6);   @circle(100,50,50);
    @grad(1);   @circle(100,50,40);
    @line(
        100 , 50 ,
        100 + @icos( gvec , 38 ) ,
        50  + @isin( gvec , 19 )
    );
]

gyajirusi_cls()
[
    @grad(0);
    @circle(100,50,40);
]


#ASM
SOUNDDATA:
_04:
    DB  0
    DW  _04_TRK1
    DW  _04_TRK2
    DW  _04_TRK3
_04_TRK1:
    DB  217, %10, 212, 17, 7, 200, 0, 7, 0, 7, 212, 17, 7, 15, 7, 17
    DB  7, 29, 7, 17, 7, 200, 0, 7, 212, 17, 14, 200, 0, 7, 212, 17
    DB  7, 15, 7, 27, 7, 29, 7, 17, 7, 200, 0, 14, 212, 17, 7, 15
    DB  7, 17, 7, 29, 7, 17, 7, 200, 0, 7, 212, 17, 14, 200, 0, 7
    DB  212, 17, 7, 15, 7, 27, 7, 29, 7, 17, 7, 200, 0, 7, 0, 7
    DB  212, 17, 7, 15, 7, 17, 7, 29, 7, 17, 7, 200, 0, 7, 212, 17
    DB  14, 200, 0, 7, 212, 17, 7, 15, 7, 27, 7, 29, 7, 17, 7, 200
    DB  0, 14, 212, 17, 7, 15, 7, 17, 7, 29, 7, 17, 7, 200, 0, 7
    DB  212, 17, 14, 200, 0, 7, 212, 17, 7, 15, 7, 27, 7, 29, 7, 19
    DB  7, 200, 0, 7, 0, 7, 212, 19, 7, 17, 7, 19, 7, 31, 7, 19
    DB  7, 200, 0, 7, 212, 19, 14, 200, 0, 7, 212, 19, 7, 17, 7, 29
    DB  7, 31, 7, 19, 7, 200, 0, 7, 0, 7, 212, 19, 7, 17, 7, 19
    DB  7, 31, 7, 19, 7, 200, 0, 7, 212, 19, 14, 200, 0, 7, 212, 19
    DB  7, 17, 7, 29, 7, 31, 7, 19, 7, 200, 0, 7, 0, 7, 212, 19
    DB  7, 17, 7, 19, 7, 31, 7, 19, 7, 200, 0, 7, 212, 19, 14, 200
    DB  0, 7, 212, 19, 7, 17, 7, 29, 7, 31, 7, 19, 7, 200, 0, 7
    DB  0, 7, 212, 19, 7, 17, 7, 19, 7, 31, 7, 19, 7, 200, 0, 7
    DB  212, 19, 14, 200, 0, 7, 212, 19, 7, 17, 7, 29, 7, 31, 7
    DB  254
_04_TRK2:
    DB  217, %10, 212, 32, 7, 200, 0, 14, 212, 32, 14, 208, 32, 7, 200, 0
    DB  21, 212, 32, 14, 208, 32, 7, 200, 0, 7, 212, 32, 14, 200, 0, 7
    DB  212, 32, 7, 200, 0, 14, 212, 32, 14, 208, 32, 7, 200, 0, 21, 212
    DB  32, 14, 58, 7, 200, 0, 7, 212, 60, 7, 32, 7, 200, 0, 7, 212
    DB  32, 7, 200, 0, 14, 212, 32, 14, 208, 32, 7, 200, 0, 21, 212, 32
    DB  14, 208, 32, 7, 200, 0, 7, 212, 32, 14, 200, 0, 7, 212, 32, 7
    DB  200, 0, 14, 212, 32, 14, 208, 32, 7, 200, 0, 21, 212, 32, 14, 60
    DB  7, 200, 0, 7, 212, 62, 7, 32, 7, 200, 0, 7, 212, 34, 7, 200
    DB  0, 14, 212, 34, 14, 208, 34, 7, 200, 0, 21, 212, 34, 14, 208, 34
    DB  7, 200, 0, 7, 212, 34, 14, 200, 0, 7, 212, 34, 7, 200, 0, 14
    DB  212, 34, 14, 208, 34, 7, 200, 0, 21, 212, 34, 14, 63, 7, 200, 0
    DB  7, 212, 62, 7, 34, 7, 200, 0, 7, 212, 34, 7, 200, 0, 14, 212
    DB  34, 14, 208, 34, 7, 200, 0, 21, 212, 34, 14, 208, 34, 7, 200, 0
    DB  7, 212, 34, 14, 200, 0, 7, 212, 34, 7, 200, 0, 14, 212, 34, 14
    DB  208, 34, 7, 200, 0, 14, 212, 34, 21, 63, 7, 34, 7, 56, 7, 34
    DB  7, 60, 7
    DB  254
_04_TRK3:
    DB  217, %10, 212, 36, 7, 200, 0, 14, 212, 36, 21, 200, 0, 14, 0, 7
    DB  212, 36, 21, 200, 0, 7, 212, 36, 14, 200, 0, 7, 212, 36, 7, 200
    DB  0, 7, 0, 7, 212, 36, 21, 200, 0, 7, 0, 14, 212, 36, 21, 200
    DB  0, 7, 212, 36, 14, 200, 0, 7, 212, 36, 7, 200, 0, 14, 212, 36
    DB  14, 207, 36, 7, 200, 0, 14, 0, 7, 212, 36, 14, 207, 36, 7, 200
    DB  0, 7, 212, 36, 7, 207, 36, 7, 200, 0, 7, 212, 36, 7, 200, 0
    DB  7, 0, 7, 212, 36, 14, 207, 36, 7, 200, 0, 7, 0, 14, 212, 36
    DB  14, 207, 36, 7, 200, 0, 7, 212, 36, 7, 207, 36, 7, 200, 0, 7
    DB  212, 38, 7, 200, 0, 14, 212, 38, 21, 200, 0, 14, 0, 7, 212, 38
    DB  21, 200, 0, 7, 212, 38, 14, 200, 0, 7, 212, 38, 7, 200, 0, 7
    DB  0, 7, 212, 38, 21, 200, 0, 7, 0, 14, 212, 38, 21, 200, 0, 7
    DB  212, 38, 14, 200, 0, 7, 212, 38, 7, 200, 0, 14, 212, 38, 21, 200
    DB  0, 14, 0, 7, 212, 38, 21, 200, 0, 7, 212, 38, 14, 200, 0, 7
    DB  212, 38, 7, 200, 0, 7, 0, 7, 212, 38, 21, 200, 0, 7, 0, 7
    DB  212, 38, 7, 207, 38, 7, 212, 38, 7, 207, 38, 7, 212, 38, 7, 207
    DB  38, 7, 212, 38, 7, 207, 38, 7
    DB  254
#END

