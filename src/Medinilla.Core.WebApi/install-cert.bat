@echo off
echo This requires JAVA JDK installed, and the 'bin' folder placed in the PATH.
echo Place the JAVA JDK folder inside an environment variable 'JAVA_HOME'
pause
keytool -import -trustcacerts -keystore "%JAVA_HOME%/lib/security/cacerts" -storepass changeit -noprompt -alias medinilla  -file .\certificate.crt
pause