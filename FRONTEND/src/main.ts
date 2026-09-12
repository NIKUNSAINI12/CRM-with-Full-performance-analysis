import { bootstrapApplication } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';

// Syncfusion Licensing
import { registerLicense } from '@syncfusion/ej2-base';
registerLicense('Ngo9BigBOggjGyl/Vkd+XU9FcVRDX3xKf0x/TGpQb19xflBPallYVBYiSV9jS3tTf0RjWX1ccnVXRWNaWU91Xg==');

import { API_BASE_URL, App } from './app/app';
import { routes } from './app/app-routing-module';

// Services and Tokens
import { API_BASE_URL_TOKEN, NOTIFICATION_SERVICE_TOKEN, TICKET_SERVICE_TOKEN } from './app/Core/injection-tokens';
import { DefaultNotificationService } from './app/Core/services/notification.service';
import { DefaultTicketService } from './app/Core/services/ticket.service';

// JWT Interceptor
import { jwtInterceptor } from './app/Core/interceptors/jwt.interceptor';

bootstrapApplication(App, {
  providers: [
    // 1. Routing
    provideRouter(routes),

    // 2. HttpClient with JWT interceptor (attaches Bearer token to every request)
    provideHttpClient(withInterceptors([jwtInterceptor])),

    // 3. Application-wide services
    { provide: API_BASE_URL_TOKEN, useValue: API_BASE_URL },
    { provide: TICKET_SERVICE_TOKEN, useClass: DefaultTicketService },
    { provide: NOTIFICATION_SERVICE_TOKEN, useClass: DefaultNotificationService },
  ]
}).catch(err => console.error(err));