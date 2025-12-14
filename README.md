# EveCommander

Windows desktop application that enumerates your running Eve Online game clients and allows you to select and run plugins against those clients.

This project contains a single "GridScout" intel plugin, which is provided to demonstrate how to write your own plugins.

Care should be taken when developing your own plugins to ensure the software only reads data from the game client.  Any automated input is likely to be in violation of the Eve Online EULA.

This software was only possible due to the amazing work of Arcitectus and their Python memory reading software named Sanderling.  The original and still maintained repository for Sanderling is here: https://github.com/Arcitectus/Sanderling

EveCommander is dependent on my fork of Sanderling, which contains a (currently partial) C# port of the original UI parser (written in elm) from Sanderling, here: https://github.com/LakMoore/Sanderling   This project is accepting Pull Requests for additional UI parsing functionality, please contribute generously.
