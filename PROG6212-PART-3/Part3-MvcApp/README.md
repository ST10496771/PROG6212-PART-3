# RaceDay Event Management System - Part 3: MVC Web Application

Student: Steffi Tanganyika (ST10496771)

ASP.NET Core 8 **MVC** front end for the RaceDay platform. It talks to the **Part 2 RESTful API** (the `RaceDayAPI` folder)
for *all* data - there is no database access or duplicated business logic in the MVC project - and the whole system
is containerised with Docker. **Azure Blob Storage** was added to the API for event banners and profile pictures.

## Links

- YouTube demo (unlisted): https://youtu.be/JHSOnPo_xUE
- Part 1 repository: https://github.com/ST10496771/PROG6212-PART-1
- Part 2 repository: https://github.com/ST10496771/PROG6212-PART-2
- Part 3 repository: https://github.com/ST10496771/PROG6212-PART-3

## CI/CD build

![Green build](docs/green-build.png)

The workflow in `.github/workflows/ci.yml` builds the API **and the MVC project**, runs all unit tests and builds both Docker images on every push.

## Run with Docker (one command)

Requirements: Docker Desktop. From the `Part3-MvcApp` folder run:

```
docker compose up --build
```

| What | URL |
|---|---|
| MVC web app | http://localhost:8081 |
| API (Swagger) | http://localhost:5162/swagger |

This starts SQL Server, an Azurite blob-storage emulator, the API and the MVC app. The first start takes a minute or two
while SQL Server boots (the API retries until the database is ready). Stop everything with `Ctrl+C`, then `docker compose down`.

### Using a real Azure Storage account
The compose file uses Azurite (a local Azure Blob Storage emulator) by default. To use your own Azure Storage account,
set the connection string before `docker compose up`:

```
# PowerShell
$env:AZURE_STORAGE_CONNECTION_STRING = "<your storage account connection string>"
# bash
export AZURE_STORAGE_CONNECTION_STRING="<your storage account connection string>"
```

## Run locally in Visual Studio (without Docker)

1. Open `RaceDayAPI/RaceDayAPI.slnx`.
2. Right-click the solution -> **Configure Startup Projects** -> **Multiple startup projects** -> set **RaceDayAPI** and **RaceDay.Mvc** to *Start*, then press **F5**.
   The API runs on `http://localhost:5162` and the website on `http://localhost:5190`.
3. The API creates the `RaceDayDB` database and adds demo data the first time it runs.
4. **Image storage:** without a connection string the site works, and image uploads show a friendly
   "storage unavailable" message. To enable uploads, right-click `RaceDayAPI` -> **Manage User Secrets** and add:
   ```json
   { "AzureStorage": { "ConnectionString": "<your storage account connection string>" } }
   ```

### Demo accounts (password for all: `Password123!`)
| Role | Email |
|---|---|
| Organiser | thabo.nkosi@raceday.co.za |
| Organiser | sarah.vdm@raceday.co.za |
| Participant | lindiwe.dube@example.com |
| Participant | james.botha@example.com |
| Participant | aisha.patel@example.com |

## How it fits together

* **MVC -> API only.** `ApiClient` is the only way the MVC app gets or changes data.
* **Sessions.** On login the API issues its session cookie. The MVC app keeps that cookie **server-side in its own
  session** (never exposed to the browser) and forwards it on every API call. A separate MVC authentication cookie
  holds the user's id and role, which drives `[Authorize(Roles = ...)]` and the navigation menu. If the API session
  ends, the user is logged out and sent to the login page. Logout clears both.
* **Role-based navigation.** Organisers see Dashboard / My Events / Browse Events. Participants see Events / My Entries /
  My Results. Visitors see Events / Log in / Register. The wrong role gets an "Access denied" page.
* **Images through the API.** Uploads go MVC -> API -> Azure Blob Storage. Images are shown through MVC image URLs
  (`/Images/EventBanner/{id}`, `/Images/ProfilePicture`) that fetch from the API, so the MVC app never talks to Azure
  and the blob container stays private.
* **Colour-coded enrolment status:** green = Confirmed, amber = Awaiting Results, blue = Completed.
* **Graceful errors.** Forms validate input, API failures become friendly messages, and unexpected errors show a
  styled error page (never raw exception text).

## Pages

**Public / Participant:** Home (upcoming events with filters for location, type and date), Event details (info, categories,
results, enrol), My Entries (status and withdraw), My Results (event, date, category, time, position such as "1st out of 3"),
Profile (details and picture), Register, Login.

**Organiser:** Dashboard (events, enrolments per event, upcoming dates), My Events (create / edit / delete with banner
upload), Categories, Enrolments, Results entry, Profile.

## New API endpoints added for Part 3 (image storage)

| Method | Route | Who |
|---|---|---|
| POST / DELETE | /api/event/{id}/banner | Organiser who owns the event |
| GET | /api/event/{id}/banner | Everyone |
| POST / GET / DELETE | /api/profile/picture | Any logged-in user (their own picture) |

Images are stored under predictable blob names (`event-banners/{eventId}` and `profile-pictures/{role}-{userId}`),
so **no database columns were added** and no new migration is needed.
