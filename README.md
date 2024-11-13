# Character Engine

**Character Engine** is a powerful aggregator of various online platforms in the form of a Discord bot that allows you to create AI-driven characters based on [Discord Webhooks](https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks) and LLM chatbots to help you bring some life and joy on your server!<br>

<div align="center">
  <b>
    [ <a href="https://discord.com/oauth2/authorize?client_id=1078278222954905660">Add Character Engine to your server</a> | <a href="https://discord.gg/JtVzgJ8Znh">Join community server: Vivarium</a> ]
    <br>
    <br>
    <img src="https://github.com/user-attachments/assets/31a67276-2acc-410a-ac1f-957b602caebc" width=1200>
    <br>
    <img height=490 src="https://github.com/user-attachments/assets/3f8c89ec-f0d0-4691-8a8f-b36f86bfc016"> 
    <img height=490 src="https://github.com/user-attachments/assets/f8a77085-2710-4cf3-bb2e-78d70b13663a"> 
  </b>
  <br>
</div>

##
### ⛳ Supported platforms
- [CharacterAI](https://character.ai/)
- [SakuraAI](https://www.sakura.fm/)

### 🕹 Features
- Allows to spawn up to 15 characters in a single channel and unlimited amount on the whole server
- Embedded characters explorer
- Per-server, per-channel and per-character flexible configurations

##
# 🚀 Self-Hosting Setup Guide

1. **Initialize Submodules**
   - Run the following commands to initialize and update the required API wrapper submodules:
     ```bash
     git submodule init
     git submodule update
     ```

2. **Configure Environment Variables**
   - Rename the `.env.example` file to `.env`:
     ```bash
     mv .env.example .env
     ```
   - Open the `.env` file and fill in the required values:
     - **DB Name**: Choose a name for your database.
     - **DB User**: Set a username for database access.
     - **DB Password**: Create a secure password for the database.

3. **Set Up Configuration Files**
   - Go to the `src/CharacterEngineDiscord/Settings` directory.
   - Open the `config` file and complete any required values. Alternatively, create a new `env.config` file based on `config` but customized with your specific settings.

4. **Install Docker (If Needed)**
   - Ensure you have Docker installed. Note that running this on **Windows** is untested and may require **WSL with Docker** installed.

5. **Start the Application**
   - Open a terminal in the root of the project directory and run:
     ```bash
     docker compose up
     ```
   
   This command will build and launch the application in Docker.
