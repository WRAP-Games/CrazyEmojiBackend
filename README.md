# Main Info

## Team Members:

- Rokas Braidokas - _MaRokas2005_
- Aistė Aškinytė - _aisteaskinyte_
- Patricija Bietaitė - _PatricijaBt_
- Vėjūnė Kamienaitė - _Vejkam_
- Vilius Kazlauskas - _Vilius-K_

Team Name: **WRAP** \
Game Name: **Crazy Emoji** \
Lead Developer: **Rokas**

---

# Project Description

Crazy Emoji is a collaborative online party game inspired by charades where players describe words using only emojis. The goal is to entertain by bringing people together in real-time game rooms where creativity and quick thinking matter.

In each round, one player is assigned a secret word and a set of emojis. That player chooses emojis to represent the word, while the rest of the players try to guess it. Points are awarded to players who guess correctly — with extra points for faster guesses.

The game supports multiple categories, leaderboards, and player profiles, making it a social and replayable experience. Crazy Emoji is designed to be easy to join, engaging and fun to play in groups.

---

# MVP:

## Alfa Version:

1. Room Is Created
2. Roles Are Assigned
3. The Selected Player Receives a Word and a Set of Emojis
4. The Selected Player Chooses Emojis to Represent the Word
5. The Remaining Players See the Chosen Emojis
6. Players Guess the Word
7. The Selected Player Marks the Correct Guesses

## Beta:

1. Login/Registration
2. Player Profile
3. Leaderboard
4. Timers for Setting Emojis and Guessing

## Final:

1. Multiple Categories
2. Player Who Answers Correctly and Faster Gets More Points
3. Nice user Interface

---

# Tech Stack

1. **Backend**: ASP.NET
2. **FrontEnd**: Angular + TypeScript
3. **Database**: PostgreSQL

---

# Branch Naming Rules

## Pattern:

`[initials]/[issueNumber]_[ShortTitle]_BasedOnMain`

*Example:*<br>
**Developer:** Rokas Braidokas<br>
**Issue:** #8<br>
**Task:** Rules for branches

*Branch name:*
`rb/8_RulesForBranches_BasedOnMain`

## Rules:

1. **Initials:** Your initials go first (rb for Rokas Braidokas).
2. **Issue Number:** Add the number of the issue (8).
3. **Short Title:** A simplified descriptive title (RulesForBranches).
4. **Base Branch:** Always indicate it is based on main (BasedOnMain).
5. **Format:** Use / to separate initials from the rest, _ for other parts.

---

# Code Formatting

### Visual Studio 2026 Insiders & Visual Studio 2022
- Go to **Tools** -> **Options** -> **Text Editor** -> **Code Cleanup**
- Press On **Configure Code Cleanup**
- Make Sure That **Profile 1** *(or **Profile 2**)* Have These Selected Fixers:
  - **Format document**
  - **Remove unnecessary imports or usings**
  - **Sort imports or usings**
  - **Apply file header preferences**
- Then Select **Profile 1** *(or **Profile 2**)* to Run on Save