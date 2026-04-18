# Social Login Setup Guide

This guide explains how to configure social login providers (Google, Facebook, LinkedIn, Instagram) for the TouchMunyun application.

## Overview

The application supports OAuth2 authentication with the following providers:
- **Google** - Direct OAuth2
- **Facebook** - OAuth2 (also used for Instagram)
- **LinkedIn** - OAuth2
- **Instagram** - Uses Facebook OAuth (Facebook owns Instagram)

## Backend Configuration

### 1. Add Configuration to `appsettings.json`

Add the following configuration to your `appsettings.json`:

```json
{
  "Authentication": {
    "Google": {
      "ClientId": "YOUR_GOOGLE_CLIENT_ID",
      "ClientSecret": "YOUR_GOOGLE_CLIENT_SECRET"
    },
    "Facebook": {
      "AppId": "YOUR_FACEBOOK_APP_ID",
      "AppSecret": "YOUR_FACEBOOK_APP_SECRET"
    },
    "LinkedIn": {
      "ClientId": "YOUR_LINKEDIN_CLIENT_ID",
      "ClientSecret": "YOUR_LINKEDIN_CLIENT_SECRET"
    }
  },
  "FrontendUrl": "http://localhost:3000"
}
```

### 2. Google OAuth Setup

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select an existing one
3. Enable the **Google+ API**
4. Go to **Credentials** → **Create Credentials** → **OAuth 2.0 Client ID**
5. Configure:
   - **Application type**: Web application
   - **Authorized redirect URIs**: 
     - `https://localhost:59400/api/auth/external/Google/callback` (Development)
     - `https://yourdomain.com/api/auth/external/Google/callback` (Production)
6. Copy the **Client ID** and **Client Secret** to `appsettings.json`

### 3. Facebook OAuth Setup

1. Go to [Facebook Developers](https://developers.facebook.com/)
2. Create a new app or select an existing one
3. Add **Facebook Login** product
4. Go to **Settings** → **Basic**
5. Add **Valid OAuth Redirect URIs**:
   - `https://localhost:59400/api/auth/external/Facebook/callback` (Development)
   - `https://yourdomain.com/api/auth/external/Facebook/callback` (Production)
6. Copy the **App ID** and **App Secret** to `appsettings.json`

**Note**: Instagram login uses the same Facebook app credentials.

### 4. LinkedIn OAuth Setup

1. Go to [LinkedIn Developers](https://www.linkedin.com/developers/)
2. Create a new app
3. In **Auth** tab, add **Redirect URLs**:
   - `https://localhost:59400/api/auth/external/LinkedIn/callback` (Development)
   - `https://yourdomain.com/api/auth/external/LinkedIn/callback` (Production)
4. Request the following permissions:
   - `r_liteprofile` (Basic profile)
   - `r_emailaddress` (Email address)
5. Copy the **Client ID** and **Client Secret** to `appsettings.json`

## Frontend Configuration

The frontend automatically uses the API URL from environment variables. Make sure `NEXT_PUBLIC_API_URL` is set correctly:

```env
NEXT_PUBLIC_API_URL=https://localhost:59400/api
```

## How It Works

1. User clicks a social login button on the login page
2. User is redirected to the provider's OAuth page
3. User authorizes the application
4. Provider redirects back to `/api/auth/external/{provider}/callback`
5. Backend extracts user information from OAuth claims
6. Backend creates or links the user account
7. Backend generates a JWT token
8. User is redirected to `/auth/callback` with the token
9. Frontend stores the token and redirects to the appropriate page

## Database Schema

The `users` table has been updated to support social login:
- `password` - Now nullable (social login users don't have passwords)
- `provider` - Stores the provider name (e.g., "Google", "Facebook")
- `provider_id` - Stores the provider's user ID
- Unique constraint on `(provider, provider_id)` to prevent duplicates

## Security Notes

- Social login users don't have passwords stored
- Accounts are automatically linked if a user with the same email exists
- JWT tokens are generated the same way as regular login
- All OAuth flows use HTTPS in production

## Testing

1. Start the backend server
2. Navigate to the login page
3. Click on a social login button
4. Complete the OAuth flow
5. You should be redirected back and logged in

## Troubleshooting

### "Invalid redirect URI" error
- Ensure the redirect URI in your OAuth provider settings matches exactly
- Check for trailing slashes and protocol (http vs https)

### "Social login failed" error
- Verify your Client ID and Client Secret are correct
- Check that the OAuth app is in "Live" mode (for production)
- Ensure the required permissions are granted

### User not created
- Check database connection
- Verify the migration ran successfully
- Check backend logs for errors

