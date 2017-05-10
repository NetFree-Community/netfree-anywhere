cd %~dp0

copy ..\nfaTray\bin\Release\nfaTray.exe zip
copy ..\nfaTray\bin\Release\Heijden.Dns.dll zip
copy ..\nfaService\bin\Release\nfaService.exe zip


del nfa.zip

7-Zip\7z.exe a -tzip nfa.zip .\zip\*
