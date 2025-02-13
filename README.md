
# DataVortex

DataVortex is software in NET 8.0 capable of exploiting different Telegram channels in order to manage the archives passing through them. It is able to exploit the archives according to your needs to extract the necessary information and use it

![image](https://github.com/user-attachments/assets/ee888d86-efcb-4c21-852e-beef121cfb23)

## Server Features

- The client transfer ID_HARDWARE, ACCOUNTS_CONFIG, API KEY & TELEGRAM  CREDS 

- The Server can ban/limit people for flooding

- SSL Certificates are taken from the main discord webhook

- SSL/TLS is used between Server/Client

- All licence keys can be managed

- Logs of every Client (Telegram accounts logs, ACCOUNTS_CONFIG, Connection counts)

- Support RAR/Zip Files 

- Support password protected archives by a list 

- Passculture & LOL API are supported (Reversed for Passculture)

- Support Discord Webhook 

## Client Features

- Supported on linux (for ID_HARDWARE)

- Easy to control

- ACOUNTS_CONFIG can be modified trough config.json

- Telegram creds can be written in TelegramCreds for automated logs


## Screenshots

- Client & Server
![image](https://github.com/user-attachments/assets/010a4776-148e-4fda-a49c-79735e6d8c30)
## Installation

You will need to install NET 8.0 to install on your linux/windows
 
```bash
  curl -o dotnet-install.sh https://builds.dotnet.microsoft.com/dotnet/scripts/v1/dotnet-install.sh && chmod +x dotnet-install.sh && bash dotnet-install.sh
  git clone https://github.com/EliasMorin/DataVortex/
```


## Usage/Examples
- You only have to double click the exetecutable file for the client and press yes to UAC Box
- If on linux, you can build a dll file and execute it 
- For the server you have to execute it trough a terminal 
- Don't forget to modify the IP/Port in client and Port in client 
```
dotnet run 

```

