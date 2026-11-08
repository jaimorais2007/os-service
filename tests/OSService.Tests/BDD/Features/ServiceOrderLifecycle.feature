Feature: Complete Service Order Lifecycle Management (OS Service)
  As a mechanical workshop operating in a microservices architecture
  I want to manage the entire lifecycle of a Service Order from creation to delivery or rejection
  So that status consistency, metrics calculation, persistence, and event notifications are guaranteed

  Scenario: Complete flow of an approved Service Order until vehicle delivery
    Given a valid customer and their vehicle are registered for opening a Service Order
    When a new Service Order is opened for the customer and vehicle
    Then the Service Order should be created with status "Received"
    And a status change notification for "Received" should have been published
    When the mechanic starts diagnostics on the Service Order
    Then the Service Order status should be updated to "UnderAnalysis"
    And a status change notification for "UnderAnalysis" should have been published
    When the mechanic finishes the technical analysis with a budget of 450.00
    Then the Service Order status should be updated to "AwaitingApproval"
    And the budget recorded on the Service Order should be 450.00
    And a status change notification for "AwaitingApproval" should have been published
    When the customer approves the Service Order budget
    Then the Service Order status should be updated to "InProgress"
    And a status change notification for "InProgress" should have been published
    When the mechanic finishes the service execution with no pending parts
    Then the Service Order status should be updated to "Completed"
    And a status change notification for "Completed" should have been published
    When the vehicle is delivered to the customer
    Then the Service Order status should be updated to "Delivered"
    And a status change notification for "Delivered" should have been published
    And the Service Order status history in the database should contain 6 status records
    And the status mean time metrics should have been recorded 5 times

  Scenario: Complete flow of a Service Order with budget rejected by the customer
    Given a valid customer and their vehicle are registered for opening a Service Order
    When a new Service Order is opened for the customer and vehicle
    And the mechanic starts diagnostics on the Service Order
    And the mechanic finishes the technical analysis with a budget of 1200.00
    And the customer rejects the Service Order budget
    Then the Service Order status should be updated to "Rejected"
    And the budget recorded on the Service Order should be 1200.00
    And a status change notification for "Rejected" should have been published
    And the Service Order status history in the database should contain 4 status records

  Scenario: Blocking execution completion when parts are pending and completing after parts arrive
    Given a Service Order is in progress with an approved budget of 800.00
    When the mechanic attempts to finish execution reporting pending parts
    Then the operation should be rejected with HTTP status 400 and message "Cannot finish a service order with pending parts."
    And the Service Order status should remain "InProgress"
    When the mechanic finishes the service execution with no pending parts
    And the vehicle is delivered to the customer
    Then the Service Order status should be updated to "Delivered"
    And the Service Order status history in the database should contain 6 status records

  Scenario: Blocking invalid status transition when attempting to deliver a vehicle before service completion
    Given a valid customer and their vehicle are registered for opening a Service Order
    When a new Service Order is opened for the customer and vehicle
    And an attempt is made to deliver the vehicle before completing the service
    Then the operation should be rejected with HTTP status 400 and message "Service order must be in 'Completed' status to be delivered."
    And the Service Order status should remain "Received"

  Scenario: Adding parts and services and creating budget for a Service Order under analysis
    Given a valid customer and their vehicle are registered for opening a Service Order
    When a new Service Order is opened for the customer and vehicle
    And the mechanic starts diagnostics on the Service Order
    And a part with price 60.00 and quantity 2 is added to the Service Order
    And a service with price 180.00 is added to the Service Order
    And the mechanic creates the budget based on added parts and services
    Then the budget recorded on the Service Order should be 300.00
    When the mechanic finishes the technical analysis with calculated budget
    Then the Service Order status should be updated to "AwaitingApproval"
    And the budget recorded on the Service Order should be 300.00

  Scenario: Receiving payment directly approves budget and advances Service Order to execution
    Given a valid customer and their vehicle are registered for opening a Service Order
    When a new Service Order is opened for the customer and vehicle
    And the mechanic starts diagnostics on the Service Order
    And the mechanic finishes the technical analysis with a budget of 500.00
    When payment of 500.00 is received for the Service Order
    Then the Service Order status should be updated to "InProgress"
    And the Service Order should be marked as paid
