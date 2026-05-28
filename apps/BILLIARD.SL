//
//     BILLIARDS  1990/4/19
//
//   Programed by Isamu Kaneko
//

// org    $5000;
// offset $4000;
// work   $d000;

CONST _TILESW=1;

#include SOROBAN.LIB
#include GRAPHF.LIB

 const   w=9,
         p1=300,
         p2=p1*2;

 var     a,b,bk,f2,f3,f4,ff,fm,hl,hl2,k,l,rf2,v,
         x2,x4,xxx,y2,y4,yyy,pf,sp;

 array   !c1[4],!c2[4],!c3[4],!c4[4],
         !dt1[4],!dt4[4],!dt10[4],!dt100[4],!dt1000[4],
         !x[9][4],!vx[9][4],!xx[9][4],!x1[4],!x3[4],!dwx[4],!rx[9][4],
         !y[9][4],!vy[9][4],!yy[9][4],!y1[4],!y3[4],!dwy[4],!ry[9][4],
         !ww[9][4],!s1[9][4],!s2[9][4],
         !dm[4],!dm2[4],!t[4],!r[4],!tt[4],!rr[4],!dw[4],!z[4],
         !f[9],!f1[9],!f5[9],!rf[9],!vram[80*44];

main()
 begin
  @single();
  pf=0; hl=0;
  repeat [
    width(80); @init(); @palet(0,1,2,3,4,7,6,7);
    for a=0 to 80*44 vram[a]=255;
    f2=1; bk=1;
    @cvitf(t,80);
    @cvitf(c1,50);  @cvitf(c2,10);
    @cvitf(c3,100); @cvitf(c4,2000);
    @cvitf(dt1,1);
    @cvitf(dt4,4);
    @cvitf(dt10,10);
    @cvitf(dt100,100);
    @cvitf(dt1000,1000);
    for a=0 to w  f[a]=1;
    @cvitf(x[0],235); @cvitf(y[0],85);
    @cvitf(x[1],85 ); @cvitf(y[1],85);
    @cvitf(x[2],67 ); @cvitf(y[2],75);
    @cvitf(x[3],49 ); @cvitf(y[3],85);
    @cvitf(x[4],67 ); @cvitf(y[4],95);
    @cvitf(x[5],76 ); @cvitf(y[5],80);
    @cvitf(x[6],76 ); @cvitf(y[6],90);
    @cvitf(x[7],58 ); @cvitf(y[7],80);
    @cvitf(x[8],58 ); @cvitf(y[8],90);
    @cvitf(x[9],67 ); @cvitf(y[9],85);
    for a=0 to w [
      @move(rx[a],x[a]);
      @move(ry[a],y[a]);
      rf[a]=f[a];
    ]
    rf2=1;

    @mask($aa55,$aa55);
    @mode(3,1);
    @full(11,0,626,6);
    @full(626,6,639,163);
    @full(11,163,626,169);
    @full(0,10,11,163);
    @circle(11,6,12);
    @circle(626,6,12);
    @circle(11,163,12);
    @circle(626,163,12);
    @mode(3,2);
    @full(418,191,630,199);
    @full(11,6,626,163);
    @full(80,198,120,199);
    @full(180,179,220,199);
    @mask($ffff,$ffff);
    @mode(0,2);
    @box(18,9,619,160);
    @box(17,9,620,160);
    for k=0 to 2 [
      @mode(3,k);
      for a=0 to 6 [
        if a!=3 [
          @full(a*75+95,3,a*75+96,3);
          @full(a*75+95,166,a*75+96,166);
        ]
      ]
      for a=0 to 2 [
        @full(6,a*37+48,7,a*37+48);
        @full(633,a*37+48,634,a*37+48);
      ]
    ]
    @mask(0,0);
    for k=1 to 2 [
      @mode(3,k);
      @circle(22,12,18);
      @circle(614,12,18);
      @circle(22,157,18);
      @circle(614,157,18);
      @circle(320,12,20);
      @circle(320,157,20);
    ]
    @mode(3,2);
    @mask($aa55,$aa55);
    @full(19,10,618,159);
    @mask($ffff,$ffff);
    for k=0 to 2 [
      @mode(3,k);
      @circle(100,189,20);
      @circle(200,189,20);
    ]

    repeat [
      @mode(3,0); @mask(0,0);
      @full(19,10,618,159);
      @mask($ffff,$ffff);
      for a=w downto 0 [
        if f[a]>=1 [
          ball(@cvfti(xx[a]),@cvfti(yy[a]),255,1);
        ]
      ]
      for a=w downto 0 [
        f1[a]=0;
        xxx=@cvfti(x[a]); yyy=@cvfti(y[a]);
        @cvitf(vx[a],0); @cvitf(vy[a],0);
        @cvitf(ww[a],0); @cvitf(s1[a],0); @cvitf(s2[a],0);
        if f[a]>=1 [
          if xxx.<.15  @cvitf(x[a],15);
          if xxx.>.304 @cvitf(x[a],304);
          if yyy.<.15  @cvitf(y[a],15);
          if yyy.>.154 @cvitf(y[a],154);
          ball(@cvfti(x[a]),@cvfti(y[a]),a,1);
        ]
      ]
      ball_number();

      xxx=1800; yyy=0; f3=0;
      @mask($ffff,$ffff);
      repeat [
        @cvitf(dm,10);
        @cvitf(x3,xxx); @div(x3,x3,dm); @rad(x3,x3);
        @sin(y3,x3); @cos(x3,x3);
        @cvitf(dm,300); @mul(x3,x3,dm); @mul(y3,y3,dm);
        @add(x3,x3,x); @add(y3,y3,y); @cvitf(dm,2); @mul(x3,x3,dm);
        x2=@cvfti(x)*2; y2=@cvfti(y); x4=@cvfti(x3); y4=@cvfti(y3);
        @mode(1,0);
        @line(x2,y2,x4,y4);
        hl2=0;
        if hl [
          @cvitf(dm,10);
          @cvitf(x3,xxx); @div(x3,x3,dm); @rad(x3,x3);
          @sin(y3,x3); @cos(x3,x3);
          @mul(x3,x3,dt10); @mul(y3,y3,dt10);
          @move(vx,x3); @move(vy,y3);
          @mul(dm,vx,vx); @mul(dm2,vy,vy);
          @add(dm,dm,dm2); @sqr(z,dm);
          @sub(x1,x,x[f2]);
          @sub(y1,y,y[f2]);
          @mul(dm,y1,vx);
          @mul(dm2,x1,vy);
          @sub(dm,dm,dm2); @div(rr,dm,z);
          @abs(dm2,rr);
          if @cmp(dm2,dt10)!=1 [
            @mul(dm2,rr,rr); @sub(dm2,dt100,dm2);
            @sqr(dm2,dm2); @neg(tt,dm2);
            @mul(dm,tt,vx); @mul(dm2,rr,vy);
            @sub(dm,dm,dm2); @div(dm,dm,z);
            @add(dwx,x[f2],dm);
            @mul(dm,rr,vx); @mul(dm2,tt,vy);
            @add(dm,dm,dm2); @div(dm,dm,z);
            @add(dwy,y[f2],dm);
            @sub(x1,x[f2],dwx);
            @sub(y1,y[f2],dwy);
            @mul(x1,x1,dt10);
            @mul(y1,y1,dt10);
            @mask($ffff,$ffff);
            help_print();
            hl2=1;
          ]
        ]
        k=inkey(1);
        @mode(1,0);
        @line(x2,y2,x4,y4);
        if hl2 [
          @mask(0,0);
          help_print();
        ]
        case k [
          27        : ball_move(1);
          '1'       : xxx--;
          '3'       : xxx++;
          '4'       : xxx=xxx-10;
          '6'       : xxx=xxx+10;
          '7'       : xxx=xxx-100;
          '9'       : xxx=xxx+100;
          'H' , 'h' : [
            if hl then hl=0; else hl=1;
          ]
          'R' , 'r' : [
            for a=w downto 0 [
              if f[a]>=1 [
                ball(@cvfti(x[a]),@cvfti(y[a]),255,1);
              ]
            ]
            for a=w downto 0 [
              if rf[a]>=1 [
                @move(x[a],rx[a]);
                @move(y[a],ry[a]);
                ball(@cvfti(x[a]),@cvfti(y[a]),a,1);
                f[a]=rf[a];
              ]
            ]
            if bk==2 bk=1;
            f2=rf2;
            ball_number();
          ]
        ]
      ] until ( k==' ' or k=='M' or k=='m' );
      repeat ; until inkey(0)==0;
      if k==' ' fm=0; else fm=1;
      x2=7; y2=7;
      repeat [
        @mode(1,2);
        @line(76+x2*2+fm*100,182+y2,96+x2*2+fm*100,182+y2);
        @line(86+x2*2+fm*100,177+y2,86+x2*2+fm*100,187+y2);
        k=inkey(1);
        @line(76+x2*2+fm*100,182+y2,96+x2*2+fm*100,182+y2);
        @line(86+x2*2+fm*100,177+y2,86+x2*2+fm*100,187+y2);
        case k [
          '4'       : if x2.>.0    x2--;
          '6'       : if x2.<.14   x2++;
          '8'       : if y2.>.fm*7 y2--;
          '2'       : if y2.<.14   y2++;
          'M' , 'm' : fm=1;
        ]
      ] until k==' ';
      x2=x2-7; y2=y2-7;
      while inkey(0)==' ' ;
      if fm then sp=7; else sp=0;
      repeat [
        @mode(1,2);
        @box(95+x2*2+fm*100,187+y2-sp+fm*7,105+x2*2+fm*100,191+y2-sp+fm*7);
        k=inkey(1);
        @box(95+x2*2+fm*100,187+y2-sp+fm*7,105+x2*2+fm*100,191+y2-sp+fm*7);
        case k [
        '8' : if sp.<.7 sp++;
        '2' : if sp.>.0 sp--;
        ]
      ] until k==' ';
      while inkey(0)==' ' ;
      ff=1;
      repeat [
        locate(yyy,22); print(" <> ");
        for a=0 to p2 a=a;
        yyy=yyy+ff;
        if yyy>73 ff=-ff;
      ] until inkey(0)==' ';
        @cvitf(dm,10);
        @cvitf(x3,xxx); @div(x3,x3,dm); @rad(x3,x3);
        @sin(y3,x3); @cos(x3,x3);
        @cvitf(dm,yyy); @mul(vx,x3,dm); @mul(vy,y3,dm);
        @cvitf(dm,4);
        @div(vx,vx,dm); @div(vy,vy,dm);
      if fm [
        sp=8-sp;
        x2=-x2; y2=-y2;
        @cvitf(dm,y2); @mul(s1,vx,dm); @cvitf(dm,x2);
        @mul(dm,dm,vy); @add(s1,s1,dm); @mul(s1,s1,dt4);
        @cvitf(dm,y2); @mul(s2,vy,dm); @cvitf(dm,x2);
        @mul(dm,dm,vx); @sub(s2,s2,dm); @mul(s2,s2,dt4);
        @div(vx,vx,dt10); @div(vy,vy,dt10);
        @cvitf(dm,sp);
        @mul(vx,vx,dm); @mul(vy,vy,dm);
      ]
      else [
        sp++;
        @mul(dm,vx,vx); @mul(dm2,vy,vy);
        @add(dm,dm,dm2); @sqr(dm,dm);
        x2=-x2;
        @cvitf(ww,x2); @mul(ww,ww,dm);
        @cvitf(dm,4); @div(ww,ww,dm);
        @cvitf(dm2,10);
        y2=-y2;
        @cvitf(dm,y2); @mul(s1,vx,dm); @cvitf(dm,sp.*.x2);
        @mul(dm,dm,vy); @div(dm,dm,dm2); @add(s1,s1,dm);
        @cvitf(dm,y2); @mul(s2,vy,dm); @cvitf(dm,sp.*.x2);
        @mul(dm,dm,vx); @div(dm,dm,dm2); @sub(s2,s2,dm);
      ]
      f[0]=2;
      for a=0 to w [
        f5[a]=0;
        @move(rx[a],x[a]);
        @move(ry[a],y[a]);
        rf[a]=f[a];
        @move(xx[a],x[a]); @move(yy[a],y[a]);
      ]
      if bk==2 bk=0;
      rf2=f2;
      ball(@cvfti(xx),@cvfti(yy),255,1);
      locate(0,23); print("          ");
      locate(yyy,22); print("    ");

      repeat [
        k=inkey(0);
        case k [
          '1' : pf=5;
          '2' : pf=3;
          '3' : pf=1;
          $1b : [ locate(0,0); stop(); ]
        ]
        if pf>2 [
          pf=pf-3;
          for a=w downto 0 [
            if f[a]==2 [
              ball(@cvfti(xx[a]),@cvfti(yy[a]),255,1);
            ]
          ]
        ]
        for a=w downto 0 [
          if f[a]==2 [
            @add(x[a],x[a],vx[a]);
            @add(y[a],y[a],vy[a]);
            @sub(dw,vx[a],s1[a]);
            @div(dm,dw,c2); @add(s1[a],s1[a],dm);
            @div(dm,dw,c3); @sub(vx[a],vx[a],dm);
            @div(dm,vx[a],t);
            @sub(vx[a],vx[a],dm);
            @sub(dw,vy[a],s2[a]);
            @div(dm,dw,c2); @add(s2[a],s2[a],dm);
            @div(dm,dw,c3); @sub(vy[a],vy[a],dm);
            @div(dm,vy[a],t);
            @sub(vy[a],vy[a],dm);
            b=ball_check(@cvfti(x[a]),@cvfti(y[a]),a);
            if ( b!=255 and f[b]>0 ) [
              @sub(x1,vx[a],vx[b]);
              @sub(y1,x[a],x[b]);
              @mul(dm,x1,y1);
              @sub(x1,vy[a],vy[b]);
              @sub(y1,y[a],y[b]);
              @mul(dm2,x1,y1);
              @add(dm,dm,dm2);
              @cvitf(dm2,0);
              if ( @cmp(dm,dm2)!=1 or f5[b]==0 ) [
                @sub(x1,x[a],x[b]);
                @sub(y1,y[a],y[b]);
                @mul(dm,x1,x1); @mul(dm2,y1,y1); @add(r,dm,dm2);
                if ( f[a]==1 or f[b]==1 ) [
                  @mul(dm,vx[a],vx[a]); @mul(dm2,vy[a],vy[a]);
                  @add(dm,dm,dm2); @sqr(z,dm);
                  @sub(x1,x[a],x[b]);
                  @sub(y1,y[a],y[b]);
                  @mul(dm,y1,vx[a]);
                  @mul(dm2,x1,vy[a]);
                  @sub(dm,dm,dm2); @div(rr,dm,z);
                  @abs(dm2,rr);
                  if @cmp(dm2,dt10)!=1 [
                    @mul(dm2,rr,rr); @sub(dm2,dt100,dm2);
                    @sqr(dm2,dm2); @neg(tt,dm2);
                    @mul(dm,tt,vx[a]); @mul(dm2,rr,vy[a]);
                    @sub(dm,dm,dm2); @div(dm,dm,z);
                    @add(x[a],x[b],dm);
                    @mul(dm,rr,vx[a]); @mul(dm2,tt,vy[a]);
                    @add(dm,dm,dm2); @div(dm,dm,z);
                    @add(y[a],y[b],dm);
                    @cvitf(r,100);
                    ball_collide();
                  ]
                ]
                else [
                  if @cmp(r,dt100)!=1 [
                    ball_collide();
                  ]
                ]
              ]
            ]

            ff=0;
            xxx=@cvfti(x[a]); yyy=@cvfti(y[a]);
            if xxx.<.15 [
              @sub(dw,vy[a],ww[a]); @mul(dw,dw,vx[a]); @div(dw,dw,c1);
              @sub(ww[a],ww[a],dw); @add(vy[a],vy[a],dw);
              @cvitf(dm,30); @sub(x[a],dm,x[a]); @neg(vx[a],vx[a]);
              ff=1;
            ]
            if xxx.>.304 [
              @add(dw,vy[a],ww[a]); @mul(dw,dw,vx[a]); @div(dw,dw,c1);
              @sub(ww[a],ww[a],dw); @sub(vy[a],vy[a],dw);
              @cvitf(dm,608); @sub(x[a],dm,x[a]); @neg(vx[a],vx[a]);
              ff=1;
            ]
            if yyy.<.15 [
              @add(dw,vx[a],ww[a]); @mul(dw,dw,vy[a]); @div(dw,dw,c1);
              @sub(ww[a],ww[a],dw); @add(vx[a],vx[a],dw);
              @cvitf(dm,30); @sub(y[a],dm,y[a]); @neg(vy[a],vy[a]);
              ff=1;
            ]
            if yyy.>.154 [
              @sub(dw,vx[a],ww[a]); @mul(dw,dw,vy[a]); @div(dw,dw,c1);
              @sub(ww[a],ww[a],dw); @sub(vx[a],vx[a],dw);
              @cvitf(dm,308); @sub(y[a],dm,y[a]); @neg(vy[a],vy[a]);
              ff=1;
            ]
            if ff==1 [
              if ( xxx.<.25  and yyy.<.25  ) ff=2;
              if ( xxx.>.295 and yyy.<.25  ) ff=2;
              if ( xxx.>.295 and yyy.>.145 ) ff=2;
              if ( xxx.<.25  and yyy.>.145 ) ff=2;
              if ( xxx.>.150 and xxx.<.170 and yyy.<.50  ) ff=2;
              if ( xxx.>.150 and xxx.<.170 and yyy.>.120 ) ff=2;
              if ff==2 [
                locate(0,23); print(a," in !");
                f[a]=0; ball(@cvfti(xx[a]),@cvfti(yy[a]),255,1);
                f1[a]=1;
                ball_number();
              ]
            ]

            if f[a] [
              ball(@cvfti(xx[a]),@cvfti(yy[a]),255,pf);
              ball(@cvfti(x[a]),@cvfti(y[a]),a,pf);
              @move(xx[a],x[a]); @move(yy[a],y[a]);
            ]
          ]
        ]

        l=0;
        for a=0 to w [
          if f[a]==2 [
            @abs(dm,vx[a]); @abs(dm2,vy[a]);
            if ( @cmp(dm,dt1)==-1 and @cmp(dm2,dt1)==-1 ) [
              @abs(dm,s1[a]); @abs(dm2,s2[a]);
              if ( @cmp(dm,dt1)==-1 and @cmp(dm2,dt1)==-1 ) [
                f[a]=1;
                @cvitf(ww[a],0); @cvitf(s1[a],0); @cvitf(s2[a],0);
                @cvitf(vx[a],0); @cvitf(vy[a],0);
                ball(@cvfti(x[a]),@cvfti(y[a]),a,1);
              ]
            ]
          ]
          if f[a]==2 l=1;
        ]
      ] until l==0;

      for a=w downto 0 [
        if f[a]>=1 [
          ball(@cvfti(x[a]),@cvfti(y[a]),a,1);
        ]
      ]
      ff=0;
      if f1[0]==1 [
        locate(0,23); print("ｽｸﾗｯﾁ !!"); beep(); ff=1; f3=f2;
      ]
      if f2!=f3 [
        locate(0,23); print("ﾌｧｰﾙ !!"); beep(); ff=1;
      ]
      if ff==1 [
        xxx=85;
        for a=1 to w [
          if f1[a]==1 [
            while vram[1680+xxx/4]!=255 xxx=xxx-10;
            @cvitf(x[a],xxx); @cvitf(y[a],85);
            f[a]=1; f1[a]=0; xxx=xxx-10;
          ]
        ]
        ball_number();
        scratch();
      ]
      f2=9;
      for a=1 to w [
        if ( f[a]>=1 and a<f2 ) f2=a;
      ]
      if f1[9]==1 [ ff=2; beep(); ]
    ] until ff==2;
  ] until 0;
 end;

ball_collide()
  begin
    if ( f3==0 and a==0 ) f3=b;
    if bk!=1 [
      @sub(x1,x[a],x[b]);
      @sub(y1,y[a],y[b]);
      @sub(dm,vx[a],vx[b]); @sub(dm2,vy[a],vy[b]);
      @mul(dm,dm,x1); @mul(dm2,dm2,y1);
      @add(dm,dm,dm2); @div(dm,dm,r);
      @mul(dm2,dm,y1); @mul(dm,dm,x1);
      @add(vx[b],vx[b],dm); @add(vy[b],vy[b],dm2);
      @sub(vx[a],vx[a],dm); @sub(vy[a],vy[a],dm2);
      @add(dwx,ww[a],ww[b]); @mul(dwx,dwx,dm2); @div(dwx,dwx,c4);
      @add(dwy,ww[a],ww[b]); @mul(dwx,dwx,dm);  @div(dwy,dwy,c4);
      @add(vx[a],vx[a],dwx); @add(ww[a],ww[a],dwx);
      @sub(vx[b],vx[b],dwx); @sub(ww[b],ww[b],dwx);
      @sub(vy[a],vy[a],dwy); @add(ww[a],ww[a],dwy);
      @add(vy[b],vy[b],dwy); @sub(ww[b],ww[b],dwy);
      if f[b]==1 ball(@cvfti(xx[b]),@cvfti(yy[b]),255,1);
      f[a]=2; f[b]=2; f5[a]=1; f5[b]=1;
    ]
    else  [
      break(); bk=2;
    ]
  end;

ball(x,y,c,f)
  begin
    var xx,yy,a;
    if ( x.>=.15 and x.<=.304 and y.>=.15 and y.<=.154 ) [
      if f!=2
        if f then ball_1(x,y,c);
        else ball_2(x,y,c);
      else
        if c!=255 ball_2(x,y,c);
      xx=x/4; yy=y/4;
      vram[yy*80+xx]=c;
    ]
  end;

ball_1(x,y,c)
  begin
    x=x.*.2;
    @mode(3,0);
    case c [
      2,4,0,9 : @mask($ffff,$ffff);
      others  : @mask(0,0);
    ]
    @circle(x,y,9);
    @mode(3,1);
    case c [
      1,3,4,5,9,0 : @mask($ffff,$ffff);
      7           : @mask($aa55,$aa55);
      others      : @mask(0,0);
    ]
    @circle(x,y,9);
    @mode(3,2);
    case c [
      1,6,9,0 : @mask($ffff,$ffff);
      5,255   : @mask($aa55,$aa55);
      others  : @mask(0,0);
    ]
    @circle(x,y,9);
    if c==9 [
      @mask(0,0); @mode(3,0);
      @full(x-9,y-2,x+9,y+1);
    ]
  end;

ball_2(x,y,c)
  begin
    x=x.*.2;
    if c==255 [
      @mode(3,0); @mask(0,0);
    ]
    else [
      @mode(2,0);  @mask($ffff,$ffff);
    ]
    @full(x-2,y-1,x+2,y+1);
  end;

ball_check(x,y,c)
  begin
    var xx,yy,b,f,d;
    xx=x/4-2; yy=y/4-2;
    b=yy*80+xx;
    f=255;
    for yy=0 to 4 [
      for xx=0 to 4 [
        d=vram[b];
        if ( d!=255 and d!=c and d<f ) f=d;
        b++;
      ]
      b=b+75;
    ]
  return(f);
  end;

break()
  begin
    var a;
    @move(dm,vx);
    @abs(dm,dm);
    for a=0 to w [
      @cvitf(vx[a],rnd(800)-400);
      @div(vx[a],vx[a],dt1000);
      @mul(vx[a],vx[a],dm);
      @cvitf(vy[a],rnd(800)-400);
      @div(vy[a],vy[a],dt1000);
      @mul(vy[a],vy[a],dm);
    ]
    for a=0 to w [
      f[a]=2; ball(@cvfti(xx[a]),@cvfti(yy[a]),255,1);
    ]
  end;

scratch()
  begin
    @mask($ffff,$ffff);
    ball(@cvfti(xx),@cvfti(yy),255,1);
    for a=w downto 1 [
      if f[a]>=1 [
        ball(@cvfti(xx[a]),@cvfti(yy[a]),255,1);
      ]
    ]
    for a=w downto 1 [
      if f[a]>=1 [
        ball(@cvfti(x[a]),@cvfti(y[a]),a,1);
      ]
    ]
    ball_move(0);
    f[0]=1;
  end;

ball_number()
  begin
    var a,c;
    for a=1 to w [
      c=a;
      locate(50+a*3,24);
      if f[a] [
        print(a);
      ]
      else [
        c=255; print(" ");
      ]
      ball_1(a*12+202,195,c);
    ]
  end;

ball_move(c)
  begin
    var kk,xxx,yyy,k,a;
    if c [
      repeat [
        repeat [
          k=inkey(1);
          k=k-'0';
        ] until k<10;
      ] until f[k];
      kk=k;
    ]
    else [
      kk=0;
      k=f2;
    ]
    @mask($ffff,$ffff);
    ball(@cvfti(x[kk]),@cvfti(y[kk]),255,1);
    xxx=@cvfti(x[k]); yyy=@cvfti(y[k]);
    repeat [
      @mode(1,0);
      @line(xxx*2-10,yyy,xxx*2+10,yyy);
      @line(xxx*2,yyy-5,xxx*2,yyy+5);
      for a=0 to p1 a=a;
      @line(xxx*2-10,yyy,xxx*2+10,yyy);
      @line(xxx*2,yyy-5,xxx*2,yyy+5);
      k=inkey(0);
      case k [
        '4' : if xxx.>.15  xxx--;
        '6' : if xxx.<.304 xxx++;
        '8' : if yyy.>.15  yyy--;
        '2' : if yyy.<.154 yyy++;
      ]
    ] until k==' ';
    @cvitf(x[kk],xxx); @cvitf(y[kk],yyy);
    ball(@cvfti(x[kk]),@cvfti(y[kk]),kk,1);
    repeat ; until inkey(0)==0;
  end;

help_print()
  var x,y;
  begin
    x=@cvfti(dwx).*.2;
    y=@cvfti(dwy);
    @mode(1,0);
    @add(dm,x1,dwx);
    @add(dm2,y1,dwy);
    @line(x,y,@cvfti(dm).*.2,@cvfti(dm2));
    @sub(dm,dwx,x1);
    @sub(dm2,dwy,y1);
    @line(x,y,@cvfti(dm).*.2,@cvfti(dm2));
    @sub(dm,dwx,y1);
    @add(dm2,dwy,x1);
    @line(x,y,@cvfti(dm).*.2,@cvfti(dm2));
    @add(dm,dwx,y1);
    @sub(dm2,dwy,x1);
    @line(x,y,@cvfti(dm).*.2,@cvfti(dm2));
    @mode(3,0);
    @circle(x,y,9);
  end;

