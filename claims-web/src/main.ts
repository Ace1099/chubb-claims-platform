
import { provideHttpClient } from '@angular/common/http';
import { bootstrapApplication } from '@angular/platform-browser';
import { ShellComponent } from './app/shell.component';

bootstrapApplication(ShellComponent, {
  providers: [provideHttpClient()]
}).catch(err => console.error(err));
