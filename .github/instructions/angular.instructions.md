---
applyTo: "**/*.ts,**/*.html,**/*.scss"
---

# Angular Development Guidelines

## Project Layout

<!-- DOT-CUSTOM-START:angular-layout -->
src/DTS.UKCT.Efiling/ClientApp/
├── projects/
│   └── web-app/              # Main application
│       └── src/app/
│           ├── _shared/      # Shared components
│           ├   ├── directives/
│           ├   ├── interceptors/
│           ├   ├── services/
│           ├   └── models/
│           └── {feature}/    # Feature modules
<!-- DOT-CUSTOM-END:angular-layout -->

## Component Generation
Generate components with SCSS and standalone:
```bash
ng generate component features/<feature>/<component-name> --style=scss --standalone=true
```

## Component Structure

<!-- DOT-CUSTOM-START:angular-component-structure -->
public and private members are grouped separately, in the following order:

```typescript
import { Component, DestroyRef, Input, Output, EventEmitter, inject } from "@angular/core";

@Component({
    selector: "app-my-component",
    standalone: true,
    imports: [],
    templateUrl: "./my-component.component.html",
    styleUrl: "./my-component.component.scss"
})
export class MyComponentComponent {

    private readonly _service = inject(MyService);
    private readonly _destroyRef = inject(DestroyRef);

    @Input() 
    public data: MyModel;
    @Output() 
    public dataChange: EventEmitter<MyModel> = new EventEmitter<MyModel>();

    public otherData?: string;

    public get displayValue(): string {
        return this.data?.name ?? "";
    }

    public onAction(): void {
        this.dataChange.emit(this.data);
    }

    private otherMethod(): void {
        // Private method logic
    }
}
```
<!-- DOT-CUSTOM-END:angular-component-structure -->

## Key Conventions

- **Standalone**: Components ARE standalone (`standalone: true`)
- **DI**: Use `inject()` function — never constructor injection
- **Constructors**: Always `public`
- **Member Accessibility**: Explicit (`public`, `private`, `protected`)
- **Return Types**: Explicit function return types required
- **Quotes**: Double quotes for strings
- **Styling**: SCSS only
- **Control Flow**: Use newer syntax for Control Flow in templates

## Observables and Subscriptions

### Naming

- Suffix **infinite** observable variables with `$` (e.g., `resize$`, `formChanges$`). These are observables that never complete on their own (e.g., `Subject`, `BehaviorSubject`, DOM event streams, router events).
- **Finite** observables (e.g., HTTP calls from `ApiRestClient`) do **not** use the `$` suffix.

### Unsubscribing

- **Infinite observables** must be unsubscribed to prevent memory leaks. Use `takeUntilDestroy(this._destroyRef)`:

```typescript
import { DestroyRef } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { Router } from "@angular/router";

...

private readonly _destroyRef = inject(DestroyRef);
private readonly _router = inject(Router);

public ngOnInit(): void {
  this._router.events.pipe(takeUntilDestroyed(this._destroyRef)).subscribe(event => {
    // handle event
  });
}
```

- **Finite observables** (typically produced by `ApiRestClient` or `HttpClient` methods like `get`, `post`, `put`, `delete`) complete after emitting and do **not** need `takeUntilDestroy()`. Subscribe directly:

```typescript
public onSave(): void {
  this._myService.createItem(this.workAreaId, this.item).subscribe(result => {
    // handle result
  });
}
```

## Styling

- Use SCSS for Angular components only
- Use kebab-casing for classes e.g. this-is-my-class.
- Use Flexbox for layout where possible.

### SCSS Imports

```scss
@use "ddx-colours" as colours;

.my-component {
    background-color: colours.$grey-100;
    border: 1px solid colours.$grey-300;
    
    &__header {
        color: colours.$primary-green;
    }
}
```

Use DDX colours in SCSS where possible:

```scss
$black: #000;
$white: #fff;
$purple: #8A0A95;
$red: #DA291C;
$orange: #ED8B00;
$yellow: #FFCD00;
$deloitte-green: #86bc25;
$primary-green-1: #43B02A;
$primary-green-2: #26890D;
$primary-green-3: #046A38;
$primary-teal: #0D8390;
$primary-blue: #007CB0;
$secondary-green-1: #E3E48D;
$secondary-green-2: #C4D600;
$secondary-green-3: #009A44;
$secondary-green-4: #2C5234;
$secondary-teal-1: #DDEFE8;
$secondary-teal-2: #9DD4CF;
$secondary-teal-3: #6FC2B4;
$secondary-teal-4: #00ABAB;
$secondary-teal-5: #0097A9;
$secondary-teal-6: #007680;
$secondary-teal-7: #004F59;
$secondary-blue-1: #A0DCFF;
$secondary-blue-2: #62B5E5;
$secondary-blue-3: #00A3E0;
$secondary-blue-4: #0076A8;
$secondary-blue-5: #005587;
$secondary-blue-6: #012169;
$secondary-blue-7: #041E42;
$gray-1: #D0D0CE;
$gray-2: #BBBCBC;
$gray-3: #A7A8AA;
$gray-4: #97999B;
$gray-5: #75787B;
$gray-6: #63666A;
$gray-7: #53565A;
$gray-8: #27282A;
$background-gray-1: #FAFAFA;
$background-gray-2: #F5F5F5;
$background-gray-3: #EBEBEB;
$bright-green: #0DF200;
$bright-teal: #3EFAC5;
$bright-blue: #33F0FF;
```

## Services

<!-- DOT-CUSTOM-START:angular-services -->

```typescript
import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { ApiRestClient } from "./rest-clients/api-rest-client.service";

@Injectable({ providedIn: "root" })
export class MyService {
    private readonly _apiClient = inject(ApiRestClient);

    public getItems(workAreaId: string): Observable<MyItem[]> {
        return this._apiClient.get<MyItem[]>(`my-feature/${workAreaId}/items`);
    }

    public createItem(workAreaId: string, item: MyItemCreate): Observable<CreatedResult> {
        return this._apiClient.post<CreatedResult>(`my-feature/${workAreaId}/items`, item);
    }
}
```

<!-- DOT-CUSTOM-END:angular-services -->

### API Cache for Static Data

Use `ApiCacheService` from `@ddx/core` to cache static or rarely-changing data:

```typescript
import { Injectable } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { Observable } from "rxjs";
import { ApiCacheService } from "@ddx/core";
import { ApiRestClient } from "./rest-clients/api-rest-client.service";

@Injectable({ providedIn: "root" })
export class JurisdictionService {
    private readonly _httpClient: HttpClient;
    private readonly _apiClient: ApiRestClient;
    private readonly _apiCache: ApiCacheService;

    public constructor(
        httpClient: HttpClient,
        apiClient: ApiRestClient,
        apiCache: ApiCacheService
    ) {
        this._httpClient = httpClient;
        this._apiClient = apiClient;
        this._apiCache = apiCache;
    }

    // Cache API responses - subsequent calls return cached observable
    public getJurisdictions(): Observable<Jurisdiction[]> {
        const url = "jurisdictions";
        return this._apiCache.get<Jurisdiction[]>(url, () => {
            return this._apiClient.get<Jurisdiction[]>(url);
        });
    }

    // Cache static asset files
    public getGeoData(): Observable<GeoData[]> {
        const url = "assets/data/world.json";
        return this._apiCache.get<GeoData[]>(url, () => {
            return this._httpClient.get<GeoData[]>(url);
        });
    }

    // Invalidate cache when data changes
    public clearJurisdictionsCache(): void {
        this._apiCache.remove("jurisdictions");
    }
}
```

**When to use ApiCacheService:**
- Reference data that rarely changes (jurisdictions, currencies, countries)
- Static asset files (JSON data files)
- Configuration data loaded at startup
- Any GET request that returns the same data for the session

## Models and DTOs

- Dtos name should match the name of the corresponding C# DTO in the API
- Dtos should be defined in a `dtos` folder, contain the `.dto.ts` suffix and "DTO" in the name (e.g. `my-item.dto.ts` with `MyItemDto` interface)
- Dtos generally belong in a `_shared` directory, a sibling to `_shared/services`
- Models typically match the name of the DTO without the "Dto" suffix, but this is not a strict requirement
- Models should be defined in a `models` folder and contain the `.model.ts` suffix
- If the model is shared across multiple files, then it should belong in a `_shared` directory, otherwise they can be defined in the same folder as the component that uses them

<!-- DOT-CUSTOM-START:angular-models-dtos -->

```typescript

export interface MyItemDto {
    id: string;
    name: string;
    createdDate: string;  // iso date string
    status: MyStatus;
}

export interface MyItem {
    id: string;
    name: string;
    createdDate: string;  // iso date string
    status: MyStatus;
    statusDisplayName: string;
}

// Inputs for creating/updating items - can be converted to DTOs if API expects different shape for create/update
export interface MyItemCreate {
    name: string;
}
```

<!-- DOT-CUSTOM-END:angular-models-dtos -->

## Enums

- Enums should be defined in a `enums` folder, contain the `.enum.ts` suffix
- If the enum is shared across multiple files, then it should belong in a `_shared` directory, otherwise they can be defined in the same folder as the component that uses them

<!-- DOT-CUSTOM-START:angular-enums -->

```typescript
export enum MyStatus {
    Draft = "Draft",
    Active = "Active",
    Archived = "Archived"
}

export function getStatusDisplayName(status: MyStatus): string {
    switch (status) {
        case MyStatus.Draft: return "Draft";
        case MyStatus.Active: return "Active";
        case MyStatus.Archived: return "Archived";
        default: return "";
    }
}
```

<!-- DOT-CUSTOM-END:angular-enums -->

## Design System (DDX/DDS)

Use DDX (`@ddx/design-system`) and DDS (`@usitsdasdesign/dds-ng`) components:

### Template Usage

```html
<dds-button 
    [label]="'Save'" 
    [type]="'primary'" 
    (clicked)="onSave()">
</dds-button>

<dds-pill 
    [label]="status" 
    [type]="getPillType(status)">
</dds-pill>

<ddx-icon [name]="'check'" [size]="'medium'"></ddx-icon>
```

### Modals

- Should have the suffix of `.modal.component.ts` (i.e. `edit-client.modal.component.ts`) and be named with the `ModalComponent` suffix (i.e. `EditClientModalComponent`)
- The name SHOULD NOT contain the word "dialog"

### DDX Select (Dropdowns)

Use `ddx-select` for all dropdown/select components. It provides searchable, customizable dropdowns with form control integration.

#### Basic Usage (Simple String Items)

For simple string arrays:

```html
<ddx-select 
    [label]="'Client'" 
    [placeholder]="'Select a client'" 
    [items]="clients" 
    [selectedItem]="selectedClient"
    [isSearchable]="true"
    (selectedItemChanged)="onSelectedClientChanged($event)">
</ddx-select>
```

```typescript
public clients: string[] = ["Client A", "Client B", "Client C"];
public selectedClient: string | null = null;

public onSelectedClientChanged(client: string | null): void {
    this.selectedClient = client;
}
```

#### With Custom Item Template (Object Items)

For complex objects, use `ng-template` with `ddxSelectItemTemplate`:

```html
<ddx-select 
    [label]="'Currency'" 
    [placeholder]="'Select currency'" 
    [items]="currencies" 
    [selectedItem]="selectedCurrency"
    [isSearchable]="true"
    [canClear]="false"
    searchPropertyName="name"
    (selectedItemChanged)="onCurrencyChanged($event)">
    <ng-template ddxSelectItemTemplate let-item>
        <div class="ddx-dropdown-item">
            <span class="ddx-dropdown-item__label">{{item.name}} ({{item.code}})</span>
        </div>
    </ng-template>
</ddx-select>
```

```typescript
public currencies: Currency[] = [
    { id: "1", name: "US Dollar", code: "USD" },
    { id: "2", name: "Euro", code: "EUR" },
    { id: "3", name: "British Pound", code: "GBP" }
];
public selectedCurrency: Currency | null = null;

public onCurrencyChanged(currency: Currency | null): void {
    this.selectedCurrency = currency;
}
```

#### With Reactive Forms

Use `formControlName` for form integration:

```html
<ddx-select 
    [label]="'Status'" 
    [items]="statuses" 
    [canClear]="false" 
    [isSearchable]="false"
    formControlName="status"
    ddxValidationMessage>
    <ng-template ddxSelectItemTemplate let-item>
        <div class="ddx-dropdown-item">
            <span class="ddx-dropdown-item__label">{{'statuses.' + item | translate}}</span>
        </div>
    </ng-template>
</ddx-select>
```

## Linting

Run before commit:

```bash
npm run lint
```