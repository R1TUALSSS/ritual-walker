# RITUAL WALKER

Advanced orbwalking tool for League of Legends with real-time attack speed tracking.

[English](#english) | [Türkçe](#türkçe)

---

## English

### Installation

1. Download `RitualWalker.exe` from [Releases](../../releases)
2. Run as Administrator
3. Configure hotkey and start

### Usage

1. Launch the program
2. Set your hotkey (default: C)
3. Adjust ping if needed
4. Start and enter a League game
5. Hold hotkey to activate orbwalking

### Building

```bash
git clone https://github.com/yourusername/ritual-walker.git
cd ritual-walker
dotnet publish RITUAL-WALKER/RITUAL-WALKER.csproj -c Release -r win10-x64 --self-contained true -p:PublishSingleFile=true -o release
```

### Disclaimer

Educational purposes only. Use at your own risk. May violate Riot Games ToS.

---

## Türkçe

### Kurulum

1. [Releases](../../releases) sayfasından `RitualWalker.exe` indirin
2. Yönetici olarak çalıştırın
3. Hotkey ayarlayın ve başlatın

### Kullanım

1. Programı başlatın
2. Hotkey'inizi ayarlayın (varsayılan: C)
3. Gerekirse ping'inizi ayarlayın
4. Başlatın ve League oyununa girin
5. Orbwalking'i aktifleştirmek için hotkey'e basılı tutun

### Derleme

```bash
git clone https://github.com/yourusername/ritual-walker.git
cd ritual-walker
dotnet publish RITUAL-WALKER/RITUAL-WALKER.csproj -c Release -r win10-x64 --self-contained true -p:PublishSingleFile=true -o release
```

### Uyarı

Yalnızca eğitim amaçlıdır. Kullanım riski size aittir. Riot Games ToS'u ihlal edebilir.
