Troubleshooting
---------------
If more than ~1000 user identities are handled then the buffer size for the Management Service needs to be increased from the default. Follow these instructions to do so...

1. Stop the QlikView Management Service 
2. Find the QVManagementService.exe.config file. 
You can find the QVManagementService.exe.config file here:  C:\Program Files\QlikView\Management Service 
3. Edit the MaxReceivedMessageSize value from 262144 to 462144 (or higher). 
4. Save the changes 
5. Restart the QlikView Management Service
