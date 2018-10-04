REM Please run cmd as administrator!

cd Client\bin\Debug &
start Client.exe &
cd ..\..\..\RepoServer\bin\Debug &
start RepoServer.exe &
cd ..\..\..\BuildServer\bin\Debug
start BuildServer.exe &
cd ..\..\..\TestHarness\bin\Debug
start TestHarness.exe &
cd ..\..\..\