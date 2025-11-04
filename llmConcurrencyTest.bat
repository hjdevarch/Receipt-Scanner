@echo off
setlocal enabledelayedexpansion

set COUNT=100

REM Record start time
for /f "tokens=2 delims==" %%I in ('wmic os get localdatetime /value') do set DTS=%%I
set START=!DTS:~0,14!

echo Launching %COUNT% concurrent requests...

for /l %%i in (1,1,%COUNT%) do (
    start "" /b curl -s -X POST ^
      "http://127.0.0.1:11434/api/generate" ^
      -H "accept: application/json" ^
      -H "Content-Type: application/json" ^
      -d "{\"model\":\"llama3\",\"prompt\":\"Categorize these items: Pomegranate,SWEETS,EX TOMATOES,MILK,Olives with Lemon,CUCUMBER,ONIONS,CUTLERY,BLUEBERRIES,Tomato Puree,FETA CHEESE,Fruit and Nut Mix,Boy's Outerwear Ja,TIGER BREAD,CHICKEN,PASTA BOWL,White Facial Tissue,CHEESE,Clear Honey,SOFT DRINK,OLIVES,GFY CFRAICHESVA,OIL,Figs 0080636,POMEGRANATE,Baby Plum Tomatoes,LAVASH,TOMATOES,STRAWBERRIES,Mini Cucumbers,Red Gala Apples,PASTA BOWL,Onions,BANANAS,LAVASH,POMEGRANATE,Seasonal Pears,WERTHERS,Large White Sourdoug,Mixed Nuts,ICE CREAM,CUTLERY,Greek Natural Yogurt,PEARS,Girl's Outerwear J,ORANGEGOLD,Baby Watermelon,POMEGRANATE,CHICKEN,CLEMENTINES,Girl's DressMD,WERTHERS,PEARS,APPLES,CUTLERY,Baking Potatoes,STRAWBERRIES,Californian Walnuts\",\"stream\":false}"
)

echo All requests launched. Waiting for completion...

REM crude wait: give processes time to finish
REM adjust timeout depending on expected response time
timeout /t 30 >nul

REM Record end time
for /f "tokens=2 delims==" %%I in ('wmic os get localdatetime /value') do set DTE=%%I
set END=!DTE:~0,14!

echo Start: %START%
echo End:   %END%

set /a SH=%START:~8,2%, SM=%START:~10,2%, SS=%START:~12,2%
set /a EH=%END:~8,2%,   EM=%END:~10,2%,   ES=%END:~12,2%

set /a STARTSEC=SH*3600+SM*60+SS
set /a ENDSEC=EH*3600+EM*60+ES

set /a ELAPSED=ENDSEC-STARTSEC
if %ELAPSED% leq 0 set /a ELAPSED=1

echo Elapsed seconds: %ELAPSED%
set /a VELOCITY=COUNT/ELAPSED
echo Velocity: %VELOCITY% requests per second

endlocal
pause